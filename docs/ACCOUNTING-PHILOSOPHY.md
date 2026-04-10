# The accounting philosophy

> "Every meaningful state change in the system is a **double-entry
> accounting operation** — a transfer of value from one party to another,
> executed under a set of analysers and interceptors, and recorded as a
> single atomic, inspectable unit."

This document explains *why* the core library is called `OperationEngine`
rather than `ServiceLayer`, *why* every controller method goes through it
rather than calling repositories directly, and *why* we think this is
worth the extra conceptual overhead.

---

## The mental model

Traditional ASP.NET Core apps look like this:

```csharp
public async Task<IActionResult> Create([FromBody] Dto req)
{
    var entity = new Thing { ... };
    await _repo.AddAsync(entity);
    await _notifier.SendAsync(...);
    await _emailer.SendAsync(...);
    return Ok(entity);
}
```

The state change is **scattered** across unrelated side-effects. If the
emailer throws, the entity is already saved. If the notifier fires but
then the save fails, you have phantom notifications. Observability is a
nightmare — "what did this request actually *do*?" requires reading the
controller, the service, maybe a listener, maybe a background job.

The accounting model collapses all of that into one shape:

```csharp
var op = Entry.Create("order.create")
    .Describe($"Order {number} from User:{customerId} to Vendor:{vendorId}")
    .From($"User:{customerId}", subtotal, ("role","customer"))
    .To($"Vendor:{vendorId}",   subtotal, ("role","vendor"))
    .Tag("pickup_type",    pickupType.ToString())
    .Tag("payment_method", paymentMethod.ToString())
    .Tag("order_number",   orderNumber)
    .Analyze(new RequiredFieldAnalyzer("items", () => req.Items))
    .Execute(async ctx =>
    {
        await _orders.AddAsync(record, ctx.CancellationToken);
        foreach (var (offer, qty) in offers)
            await _items.AddAsync(new OrderItem { ... }, ctx.CancellationToken);
        ctx.Set("orderId", record.Id);
    })
    .Build();

var envelope = await _engine.ExecuteEnvelopeAsync(op, record, ct);
```

What this gives you:

1. **Intent is explicit.** The operation has a `Type`, a human-readable
   `Description`, and structured `Parties` (who gave what to whom).
2. **Data is typed.** `Parties`, `Tags`, `Analyzers` are all value objects,
   not strings in a dictionary.
3. **Cross-cutting code runs automatically.** Any interceptor registered
   with a matching predicate (e.g. "runs on operations tagged with
   `quota_check`") fires before/after/around `Execute`. This is how
   Ashare enforces subscription quotas without a single `if` in the
   controller.
4. **Success and failure are uniform.** The engine returns an
   `OperationResult` with `Success`, `Error`, `Interceptors`, and the
   `Context` (which is a key/value bag that analyzers/interceptors/
   execute bodies can all read and write). Wrapping the result in
   `OperationEnvelope<T>` gives a single wire format.
5. **The state change is atomic.** If any pre-interceptor throws or
   returns an error, `Execute` never runs. If `Execute` throws, the
   engine records the failure cleanly — no half-state.
6. **Observability is free.** The engine logs each operation's id, type,
   status, elapsed time, and failed-interceptor name (if any). You can
   add any further hook (metrics, OpenTelemetry spans, audit writes)
   without touching controllers.

---

## The four ingredients of an operation

### 1. Parties — the double entry

```csharp
.From("User:abc", 100, ("role","sender"),   ("currency","SAR"))
.To(  "Vendor:xy", 100, ("role","recipient"),("currency","SAR"))
```

Parties are strings (format is free but conventional: `EntityKind:id`)
plus an amount and a set of `(key, value)` tags. **At least one `From`
and one `To` is required**, mirroring double-entry accounting.

"Amount" is whatever makes sense for the op. For `order.create` it's
the subtotal. For `message.send` or `notify.send` it's typically `1`
(one message, one notification). The engine doesn't enforce the
arithmetic — that's a choice we left to the domain. But having the
field means later interceptors (e.g. a subscription quota) can read
it uniformly.

### 2. Tags — the metadata

```csharp
.Tag("pickup_type", "Curbside")
.Tag("payment_method", "Cash")
.Tag("quota_check", "ListingsCreate")  // triggers the subscription interceptor
```

Tags are a flat string/string dictionary on the operation. They're
used by:
- **Interceptors** to decide whether they apply (`op.HasTag("quota_check")`).
- **Analyzers** to validate preconditions.
- **Consumers** of the envelope (e.g. a UI that wants to show the
  operation's pickup type).

The convention is: if two different operation types need to be filtered
by the same cross-cutting concern, they should share a tag.

### 3. Analyzers — precondition checks

```csharp
.Analyze(new RequiredFieldAnalyzer("content", () => req.Content))
.Analyze(new PatternAnalyzer("phone", () => req.Phone, @"^\+?[0-9]+$"))
```

Analyzers run **before** the `Execute` body. An analyzer is a small
object with a name and an `AnalyzeAsync(OperationContext)` method that
returns success or a structured failure. Built-ins include:

- `RequiredFieldAnalyzer(name, getter)` — the getter must return a
  non-null, non-empty value.
- `PatternAnalyzer(name, getter, regex)` — value must match a regex.
- Domain-specific analyzers (the apps define their own).

If any analyzer fails, the engine sets `FailedAnalyzer` on the result
and skips `Execute`. The envelope carries the failure as a clean wire
error.

**The point of analyzers vs. controller-side validation**: analyzers
run inside the engine, so any caller (HTTP controller, background job,
other operation) enforces the same rules. Validation is **attached to
the operation**, not to the HTTP layer.

### 4. Interceptors — cross-cutting logic

Interceptors are registered **globally** at startup via the
`ACommerce.OperationEngine.Interceptors` registry. They have three phases:

- `Pre` — runs before `Execute`, can short-circuit.
- `Post` — runs after a successful `Execute`.
- `Wrap` — wraps `Execute` (for things like transactions).

Each interceptor has a predicate that decides whether it applies to a
given operation (usually based on tags or type). See Ashare.Api's
`Program.cs` for a real example where subscription quota enforcement
is added without any controller ever knowing about it:

```csharp
builder.Services.AddOperationInterceptors(registry =>
{
    registry.Register(new PredicateInterceptor(
        name: "QuotaInterceptor",
        phase: InterceptorPhase.Pre,
        appliesTo: op => op.HasTag("quota_check"),
        intercept: async (ctx, _) =>
        {
            var inner = ctx.Services
                .GetRequiredService<QuotaInterceptor>();
            return await inner.InterceptAsync(ctx, null);
        }));
});
```

Every controller that creates a listing just adds `.Tag("quota_check",
"ListingsCreate")` to its operation. The quota check, the quota
consumption, the error envelope — all free.

---

## How to think about a new feature

When adding a new feature, the question you ask is **not**:

> "What controller, service, and repository do I need?"

It's:

> "What is the operation?  
> &nbsp;&nbsp;&nbsp;Who gives what to whom?  
> &nbsp;&nbsp;&nbsp;What must be true before it runs?  
> &nbsp;&nbsp;&nbsp;What side effects does it require?  
> &nbsp;&nbsp;&nbsp;What cross-cutting rules apply?"

Then you express the answer as an `Entry.Create(...)` pipeline and the
engine takes care of the rest.

### Worked example — "User sends a chat message"

- **Type**: `message.send`
- **From**: `User:{senderId}` with amount `1` (one message), role `sender`
- **To**: `User:{recipientId}` with amount `1`, role `recipient`, delivery `pending`
- **Tags**: `conversation_id`, `message_type` (text/image/…)
- **Analyzers**: `content` field is required, `content` length ≤ 2000
- **Interceptors**: if sender is a vendor → subscription quota check
  (via the `quota_check` tag on messages-send)
- **Execute**: save the Message, update the Conversation's
  LastMessageSnippet, broadcast over realtime to the recipient
- **Envelope data**: the saved `Message` object

Looking at `Ashare.Api/Controllers/MessagesController.cs` you'll see
exactly this shape.

### Worked example — "Customer places an order"

- **Type**: `order.create`
- **From**: `User:{customerId}` with amount = subtotal, role `customer`
- **To**: `Vendor:{vendorId}` with amount = subtotal, role `vendor`
- **Tags**: `pickup_type` (InStore / Curbside), `payment_method`
  (Cash / Card), `order_number`
- **Analyzers**: items non-empty, each offer exists, all offers are
  from the same vendor (enforced inside the body rather than in an
  analyzer because it needs DB access, but could be an analyzer)
- **Interceptors**: *none currently* — this is where you'd add
  inventory decrement, loyalty-points accrual, fraud detection,
  whatever your business needs.
- **Execute**: save the OrderRecord, save all OrderItems, emit a
  domain event (future: via the messaging library)
- **Envelope data**: `{ Id, OrderNumber, Total, VendorName, … }`

Looking at `Apps/Order.Api/Controllers/OrdersController.cs` you'll
see exactly this shape.

---

## Why "accounting"?

Double-entry accounting has survived from 13th-century Italy to
today's cloud ERPs for one reason: **it's self-checking**. Every
credit has a debit. Every change is a pair. The books either balance
or they don't, and when they don't you know something is wrong.

Software state changes have exactly the same property when you model
them this way. Every operation has a sender, a receiver, a reason,
and a proof. The engine doesn't care whether your currency is money
or messages or quota units — it just wants the double entry.

The practical payoff is:

- **Audit**: you can replay any operation's history. The engine
  records its type, parties, tags, analysers, interceptors, result,
  timestamps, and elapsed time.
- **Policy**: cross-cutting business rules (quotas, permissions, rate
  limits) become interceptors. You change them once.
- **Testability**: an operation is a value object, so tests construct
  one, pass it to a fake engine, and assert on the result.
- **Evolution**: the wire format (`OperationEnvelope<T>`) carries the
  operation's metadata alongside the data, so clients can show the
  operation's status, error, failed analyzer, etc. uniformly.

---

## Reporting operations and one-sided entries

Not every operation has a reversal. Not every entry requires balanced
debits and credits. The accounting model supports **reporting entries**
— operations that record a fact without expecting a counter-entry.

### View counts as accounting operations

A "page view" is an operation: a viewer transfers attention to a
listing. There is no reversal (you can't "un-view" something), and
the amounts don't need to balance. This is a **one-sided entry**:

```csharp
var op = Entry.Create("listing.view")
    .Describe($"View listing {id}")
    .From("Viewer:anonymous", 1, ("role", "viewer"))
    .To($"Listing:{id}", 1, ("role", "listing"))
    .Tag("listing_id", id.ToString())
    .Execute(async ctx =>
    {
        listing.ViewCount++;
        await _repo.UpdateAsync(listing, ctx.CancellationToken);
    })
    .Build();
```

The `From` and `To` amounts are both 1 (one view), so the
`BalanceAnalyzer` passes. But conceptually this is a *reporting*
entry — its value comes from the **count of operations**, not from
the amounts. A reporting interceptor can watch for `listing.view`
operations and produce aggregate statistics without touching the
entity's `ViewCount` field at all.

### The reporting interceptor pattern

Reporting accounts (المتاجرة، المبيعات، الموازنة، الإقفال) in
traditional accounting are themselves journal entries — but they're
*derived* entries that summarise other entries. In the OpEngine, this
maps to **post-phase interceptors** that watch specific operation types
and maintain aggregate counters or produce summary entries:

```csharp
registry.Register(new TaggedInterceptor(
    name: "ViewCountReporter",
    watchedTag: "listing_id",
    phase: InterceptorPhase.Post,
    intercept: async (ctx, _) =>
    {
        var listingId = ctx.Operation.GetTagValue("listing_id");
        // Aggregate: count all listing.view operations for this listing
        // Store in a reporting table, cache, or summary entry
        return AnalyzerResult.Pass();
    }));
```

This pattern applies to any metric:
- **Chat read receipts**: `message.read` entries (no reversal). A
  reporting interceptor counts how many recipients have read.
- **Presence tracking**: `session.enter` and `session.leave` entries.
  A reporting interceptor counts active sessions (entries without
  matching leave entries).
- **Revenue summaries**: a post-interceptor on `order.deliver` that
  accumulates daily totals into a summary entity.

The key principle: **the individual entry is atomic and immutable**.
The aggregate is a *derived view* maintained by interceptors. This
matches how real accounting works — you never modify a journal entry;
you post a new entry to adjust, and the balance sheet is always
computed from the sum of all entries.

### When to skip the BalanceAnalyzer

The `AccountingBuilder` (via `Entry.Create`) auto-adds a
`BalanceAnalyzer` that checks `debit_total == credit_total`. For
reporting entries where amounts intentionally differ, use the
low-level `OperationBuilder.Create()` directly, or use `.Sealed()`
to suppress the balance check. But in practice, keeping both sides
equal (1:1 for views, messages, presence) is cleaner — the reporting
value comes from the *count* of operations, not from asymmetric
amounts.

---

## What the engine is *not*

- **Not an ORM.** Entities and repositories live in the SharedKernel
  layer. The engine only cares about the operation.
- **Not a workflow engine.** Operations are short-lived and single-step.
  For multi-step flows (saga, long-running), build a parent operation
  whose `Execute` body orchestrates several child ops via the engine.
- **Not a message bus.** Realtime broadcasts are a *side effect* of an
  operation's execute body. They are not the operation.
- **Not magic.** If you don't model your feature as an operation, you
  lose nothing — you can still write plain controller code. But then
  you lose the interceptors, the analyzers, the envelope, and the
  audit. The library's value comes from adopting the discipline.

---

## Further reading

- `libs/backend/core/ACommerce.OperationEngine/Core/OperationBuilder.cs`
  — the fluent API you'll be using.
- `libs/backend/core/ACommerce.OperationEngine/Patterns/AccountingPattern.cs`
  — a higher-level builder for accounting-flavoured operations.
- `libs/backend/core/ACommerce.OperationEngine.Wire/OperationEnvelope.cs`
  — the wire format.
- `Apps/Ashare.Api/Controllers/ListingsController.cs` and
  `Apps/Order.Api/Controllers/OrdersController.cs` — two real-world
  usages with different domain shapes.
