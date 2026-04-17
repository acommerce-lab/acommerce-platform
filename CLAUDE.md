# ACommerce Platform — Agent Onboarding

## What this is

A .NET 10 / Blazor platform for producing production-ready multi-vendor
e-commerce applications in days. The core idea: **every state change is a
double-entry accounting operation**. Read `docs/MODEL.md` before writing
any code.

## Read order

1. **`docs/MODEL.md`** — the Operation-Accounting Model (OAM). This is the
   intellectual foundation. Without it you will write code that bypasses the
   engine and defeats the platform's purpose.
2. **`docs/ARCHITECTURE.md`** — the library map (4 layers, 40 projects).
3. **`docs/LIBRARY-ANATOMY.md`** — how domain libraries are structured
   (three-layer pattern: pure accounting + provider contracts + injected
   interceptors).
4. **`docs/BUILDING-A-BACKEND.md`** — step-by-step recipe for a new backend.
5. **`docs/BUILDING-A-FRONTEND.md`** — step-by-step recipe for a new Blazor
   frontend.
6. **`docs/ROADMAP.md`** — what's done, what's next, modification plan.

Reference apps (read the smallest first):
- `Apps/Order.Api` + `Apps/Order.Web` — cafe deals, cleanest example.
- `Apps/Vendor.Api` + `Apps/Vendor.Web` — vendor-side microservice.
- `Apps/Ashare.Api` + `Apps/Ashare.Web` — property classifieds, larger.

## The five laws

### Law 1 — Every state change is an operation

Never write `_repo.AddAsync(entity)` from a controller. Every mutation:

```csharp
var op = Entry.Create("thing.create")
    .From($"User:{ownerId}", 1, ("role", "owner"))
    .To($"Thing:{id}",       1, ("role", "created"))
    .Tag("name", name)
    .Analyze(new RequiredFieldAnalyzer("name", () => name))
    .Execute(async ctx => await _repo.AddAsync(entity, ctx.CancellationToken))
    .Build();
var envelope = await _engine.ExecuteEnvelopeAsync(op, entity, ct);
```

### Law 2 — Every response is an OperationEnvelope

Read endpoints too: `return this.OkEnvelope("thing.list", data);`

### Law 3 — Repository method signatures

- `ListAllAsync(ct)` — takes CancellationToken.
- `GetAllWithPredicateAsync(predicate)` — does NOT take CancellationToken.
  Second arg is `bool includeDeleted`. Mixing them is the #1 bug.

### Law 4 — The widgets cascade is the styling system

Use `var(--ac-primary)` or Bootstrap classes. Never hard-coded colours.
Brand overrides go in the app's `wwwroot/app.css` on `:root`.

### Law 5 — Auth state survives reload via OnAfterRenderAsync

Load auth-dependent data in `OnAfterRenderAsync(firstRender: true)` after
`await Auth.EnsureRestoredAsync()`. Not in `OnInitializedAsync`.

### Law 6 — Adapt to real data, never bend it

When integrating with an existing production system, the NEW platform adapts
to the shape of production data — not the other way around. If the
stakeholder's listings use `features` instead of `amenities`, rename the
template. If the production API returns a plain array instead of
OperationEnvelope, handle both. Any attribute key not in the template is
preserved as a raw `DynamicAttribute` entry. We serve the stakeholder's
existing data exactly as they expect it; the platform is a tool, not an
authority over business data.

## Constraint vocabulary — when to use what

| Tool | When | Example |
|---|---|---|
| **Analyzer** (`.Analyze()`) | Constraint local to ONE operation | `RequiredFieldAnalyzer("content")` |
| **Interceptor** (registry) | Cross-cutting, shared across operations | `QuotaInterceptor` on `tag("quota_check")` |
| **Validate** (`.Validate()`) | Needs DI/DB access, still local | Check all items from same vendor |
| **PredicateAnalyzer inline** | One-liner not worth a class | `.Analyze(new PredicateAnalyzer("x", ctx => ...))` |
| **ProviderContract** | Mandatory external dependency | `IMessageStore`, `IPaymentGateway` |
| **Sealed** (`.Sealed()`) | Block ALL interceptors | Sensitive internal operations |
| **ExcludeInterceptor** | Block ONE interceptor by name | Skip audit on health checks |

## Boundaries

**Do freely**: read any file; add entities/controllers/services/pages/tests/docs;
create libraries under `libs/`; build and run locally.

**Ask first**: push to main; create PR; force-push; delete files you didn't
create; modify root `.gitignore`/`LICENSE`/`README.md`; add large packages;
add production providers (payment/SMS/firebase/cloud storage).

## The canonical mental model

> "If I can't express this feature as one operation with one sender, one
> receiver, one tag set, and one execute body, I'm modelling it wrong."

Every temptation to add a service class, event bus, or background job — ask
first: is this an interceptor on an existing operation? A child operation
inside a parent's execute body? A provider contract?

## Language

Work in Arabic. Keep code identifiers, file paths, function names, and log
output in English.
