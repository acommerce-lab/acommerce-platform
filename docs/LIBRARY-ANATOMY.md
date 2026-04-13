# Library anatomy — how to build on the OAM

Every domain library built on the Operation-Accounting Model has the same
three-layer internal structure. This document defines these layers and shows
how they compose when a consuming application assembles its feature set.

---

## The three layers of a domain library

```
┌─────────────────────────────────────────────────┐
│  Layer 3: Injected interceptors                  │
│  (registered by the consuming application)       │
│  QuotaCheck, AuditLog, Translation, RateLimit    │
│  → optional, global, transparent to the library  │
└─────────────────────┬───────────────────────────┘
                      │ injected via Tags at runtime
┌─────────────────────▼───────────────────────────┐
│  Layer 2: Provider contracts                     │
│  (defined by the library, implemented outside)   │
│  IMessageStore, IDeliveryTransport, ISmsSender   │
│  → mandatory, typed, called from Execute body    │
└─────────────────────┬───────────────────────────┘
                      │ resolved from DI inside Execute
┌─────────────────────▼───────────────────────────┐
│  Layer 1: Pure accounting structure              │
│  Entry definitions + local analyzers + relations │
│  → self-contained, no external dependencies      │
└─────────────────────────────────────────────────┘
```

### Layer 1 — Pure accounting structure

The library defines **what entries exist** and **what local constraints they
have**. This layer has zero external dependencies.

Contents:
- **Entry type catalog**: `message.send`, `message.deliver`, `message.read`,
  `conversation.create`, etc.
- **Local analyzers**: `RequiredFieldAnalyzer("content")`,
  `MaxLengthAnalyzer("content", 2000)`, etc.
- **Relations**: `message.deliver` fulfills `message.send`;
  `message.read` fulfills `message.deliver`.
- **Tags convention**: which tags each entry type carries and what they mean.

Example (chat library, layer 1):

```csharp
public static class ChatEntries
{
    public static Operation SendMessage(string senderId, string recipientId,
        string content, Guid conversationId) =>
        Entry.Create("message.send")
            .From($"User:{senderId}", 1, ("role", "sender"))
            .To($"User:{recipientId}", 1, ("role", "recipient"))
            .Tag("conversation_id", conversationId.ToString())
            .Tag("requires_persistence", "true")
            .Tag("requires_delivery", "true")
            .Analyze(new RequiredFieldAnalyzer("content", () => content))
            .Analyze(new MaxLengthAnalyzer("content", () => content, 4000))
            .Build();
}
```

This layer is **testable in isolation**: construct the entry, run it through
a mock engine, assert on analyzer results. No database, no HTTP, no DI.

### Layer 2 — Provider contracts

The library defines **what external capabilities it needs** as interfaces.
These are the mandatory dependencies that must be supplied by whoever
consumes the library.

Contents:
- **Persistence contract**: how entries and their data are stored
- **Transport contract**: how effects are delivered (messages, notifications)
- **Integration contract**: how the library communicates with external systems

Example (chat library, layer 2):

```csharp
public interface IChatPersistence
{
    Task<Message> SaveMessageAsync(Message msg, CancellationToken ct);
    Task<Conversation> GetOrCreateConversationAsync(
        Guid user1, Guid user2, CancellationToken ct);
    Task MarkReadAsync(Guid conversationId, Guid readerId, CancellationToken ct);
}

public interface IChatDelivery
{
    Task DeliverAsync(Message msg, CancellationToken ct);
}
```

The library also defines the **wiring** that connects entries to providers:

```csharp
public static class ChatOperations
{
    public static Operation SendMessage(string senderId, string recipientId,
        string content, Guid conversationId, Message msg) =>
        Entry.Create("message.send")
            .From($"User:{senderId}", 1, ("role", "sender"))
            .To($"User:{recipientId}", 1, ("role", "recipient"))
            .Tag("conversation_id", conversationId.ToString())
            .Analyze(new RequiredFieldAnalyzer("content", () => content))
            .Execute(async ctx =>
            {
                var store = ctx.Services.GetRequiredService<IChatPersistence>();
                var delivery = ctx.Services.GetRequiredService<IChatDelivery>();
                var saved = await store.SaveMessageAsync(msg, ctx.CancellationToken);
                await delivery.DeliverAsync(saved, ctx.CancellationToken);
                ctx.Set("message", saved);
            })
            .Build();
}
```

Provider contracts are **interface definitions in the library, implementations
in the consumer**. The library ships with zero implementations. The consumer
picks the right one:

- `IChatPersistence` → `SqliteChatStore` or `RedisChatStore` or `InMemoryChatStore`
- `IChatDelivery` → `WebSocketDelivery` or `FcmDelivery` or `NoOpDelivery`

### Layer 3 — Injected interceptors

These are NOT defined by the library. They are registered by the consuming
application and applied automatically via tag matching.

The library's only responsibility is to **tag entries appropriately** so the
application's interceptors can match them:

```csharp
// Library tags the entry
.Tag("quota_check", "MessagesCreate")

// Application registers the interceptor
registry.Register(new PredicateInterceptor(
    name: "QuotaInterceptor",
    phase: InterceptorPhase.Pre,
    appliesTo: op => op.HasTag("quota_check"),
    intercept: async (ctx, _) => { /* check subscription */ }));
```

Common injected interceptors:
- **Quota enforcement**: check subscription limits before execution
- **Audit logging**: record all entries in an audit table after execution
- **Translation**: translate text fields before returning to client
- **Rate limiting**: throttle entries by client identity
- **Content filtering**: scan text content before persistence
- **Entry journaling**: persist the entry itself (not just its side effects)
  for replay and audit

---

## How libraries compose in an application

An application picks libraries and wires them:

```
Order.Api picks:
  libs/backend/auth/     → provides TokenAuthenticator as auth provider
  libs/backend/messaging → provides SqliteStore + InMemoryTransport
  libs/backend/sales/    → provides no payment gateway (cash only)

  Then registers interceptors:
    VendorAuditLogger (Post, all operations)
    WorkScheduleGate  (Pre, tag: vendor_order)
    AcceptanceGate    (Pre, tag: vendor_order AND tag: receive)
```

The application's `Program.cs` is the **composition root**: it registers
entities, provider implementations, and interceptors. The libraries provide
the entry definitions, analyzers, and provider contracts.

---

## Existing libraries mapped to this anatomy

### Authentication (`libs/backend/auth/`)

| Layer | Contents |
|---|---|
| L1 — Entries | `auth.register`, `auth.login`, `auth.2fa.request`, `auth.2fa.verify`, `auth.token.refresh`, `auth.logout` |
| L1 — Analyzers | `RequiredFieldAnalyzer("phone")`, `PatternAnalyzer("phone", regex)` |
| L2 — Contracts | `IAuthenticator`, `ITokenIssuer`, `ITokenValidator`, `ITwoFactorChannel` |
| L2 — Providers | `TokenAuthenticator`, `JwtTokenStore`, `SmsTwoFactorChannel`, `EmailTwoFactorChannel`, `NafathChannel` |
| L3 — Interceptors | `PermissionInterceptor` (checks role tags) |

### Messaging (`libs/backend/messaging/`)

| Layer | Contents |
|---|---|
| L1 — Entries | `message.send`, `notification.send`, `notification.read` |
| L2 — Contracts | `IRealtimeTransport`, `INotificationChannel` |
| L2 — Providers | `InMemoryRealtimeTransport`, `InAppNotificationChannel`, `FirebaseNotificationChannel` |
| L3 — Interceptors | (none built-in — quota check injected by app) |

### Subscriptions (`libs/backend/marketplace/`)

| Layer | Contents |
|---|---|
| L1 — Entries | `subscription.create`, `subscription.renew`, `subscription.cancel` |
| L2 — Contracts | `ISubscriptionProvider` |
| L3 — Interceptors | `QuotaInterceptor` (Pre, checks remaining quota), `QuotaConsumptionInterceptor` (Post, decrements quota) |

Note: Subscriptions is unusual because its **interceptors are the main
product** — other libraries tag entries with `quota_check` and the
subscription interceptors enforce limits. This is the cross-cutting
pattern at its purest.

---

## Graphical widgets as OAM entities

Widgets and templates are currently "passive" — they accept callbacks and
render UI. The OAM model suggests they should be **active**: bound to
entities from the same model, emitting operations directly, and updating
via the same state cycle.

### The idea

Instead of:
```razor
<AcLoginPage OnRequestOtp="@HandleOtp" Phone="@phone" ... />
@code {
    async Task HandleOtp() {
        var result = await Api.PostAsync("/api/auth/sms/request", ...);
        // manually update state
    }
}
```

Templates emit operations:
```razor
<AcLoginPage Store="@Store" Engine="@Engine" />
```

The template internally:
1. Reads state from `AppStore` (phone, step, error, busy)
2. On user action, calls `Engine.ExecuteAsync(ClientOps.RequestOtp(phone))`
3. The interpreter updates `AppStore`
4. The template re-renders from the new state

**No code-behind. No manual HTTP. No setState.**

### Why this works with OAM

The model IS a state-update model. Widgets are state consumers. The natural
binding is:
- Widget **reads** from `AppStore` (derived from party aggregations)
- Widget **writes** via `ClientOpEngine` (emits entries)
- State **updates** via interpreters (entry → store mutation)

This is the same cycle as the backend (entry → engine → interceptors →
result) but on the client side (entry → client engine → dispatch →
interpreter → store → re-render).

### Composition benefits

When templates are operation-aware:
- **Portability**: `AcLoginPage` works in ANY app that registers auth
  entries and an auth interpreter. No app-specific wiring needed.
- **Composition**: drop `AcCartPage` + `AcCheckoutPage` into an app, register
  `CartInterpreter` + `OrderRoutes`, done. The templates know how to emit
  the right entries.
- **Customization**: override behavior by registering different interceptors
  or interpreters. The template doesn't change.
- **Testing**: render the template with a mock engine and assert on emitted
  operations. No HTTP mocking needed.

### What needs to change

See `docs/ROADMAP.md` section "Phase 2: Operation-aware templates" for the
specific modification plan.
