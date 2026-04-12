# Session 2 Summary — Operation-Accounting Programming Model

> This document captures the decisions, implementations, and lessons from
> the second working session on the ACommerce Platform. The first session
> built the foundation (core libraries, two demo apps, templates cascade).
> This session went deeper: enforcing the accounting discipline end-to-end,
> separating vendor services, building client-side OpEngine integration,
> and defining the programming model itself.

---

## 1. What was accomplished

### 1.1 Icon system + professional typography
- **AcIcon.razor**: 35+ inline SVG line icons (Lucide-style, currentColor).
  Every emoji chrome icon in every template replaced with AcIcon.
- **Tajawal + Inter fonts**, 14px base, tighter heading sizes.
- **Neutral-first theme**: brand color reserved for CTAs only; 90% of the
  surface is white/gray. Proper dark mode with JS interop for `<html>`
  attribute flipping.

### 1.2 Search + map page
- **AcMapSearchPage.razor**: self-contained mini-map (no tile dependencies),
  filter bar, mode toggle (vendors/offers), bottom sheet, list view.
- **MapPinDto**: vertical-agnostic pin model usable by any app.
- Advanced filters: discount checkbox, price range, minimum rating,
  active filter chips.

### 1.3 Vendor.Api — separate microservice
- Own SQLite database (`data/vendor.db`), own OpEngine instance.
- Entities: `IncomingOrder`, `VendorSettings`, `WorkSchedule`.
- **Interceptors**:
  - `WorkScheduleGate` (Pre): rejects orders outside work hours.
  - `AcceptanceGate` (Pre): rejects when vendor turned off acceptance
    or hit max concurrent pending.
  - `VendorAuditLogger` (Post): logs all vendor operations.
- **OrderTimeoutService** (IHostedService): auto-cancels pending orders
  after the configured timeout.
- Inter-service webhooks: Order.Api → Vendor.Api (incoming), Vendor.Api
  → Order.Api (callback with accept/reject/ready/deliver/timeout).

### 1.4 100% OpEngine coverage across all backends
- **Audit**: 3 background agents scanned every `.cs` file in Order.Api,
  Vendor.Api, and Ashare.Api. Found ~60 total mutations, of which ~30
  bypassed OpEngine.
- **Fix**: every single DB mutation now goes through `Entry.Create → 
  From/To → Tag → Execute → Build → ExecuteAsync`. Zero exceptions.
- **Reporting operations**: `listing.view` modeled as a one-sided
  accounting entry; documented the reporting-interceptor pattern for
  aggregate metrics in `ACCOUNTING-PHILOSOPHY.md`.

### 1.5 Client-side OpEngine (ClientOpEngine) migration
- **Order.Web**: fully migrated to `AppStore` + `ClientOpEngine` +
  `HttpDispatcher` + `OperationInterpreterRegistry<AppStore>`.
  - Removed: `OrderApiClient`, `CartService`, `AuthStateService`, 
    `UiPreferences`.
  - Added: `AppStore.cs`, `ClientOps.cs`, `OrderRoutes.cs`,
    `AuthInterpreter.cs`, `CartInterpreter.cs`, `UiInterpreter.cs`,
    `ApiReader.cs`, `AppStateApplier.cs`.
  - Every page injects `AppStore` for state, `ClientOpEngine` for
    mutations, `AppStateApplier` for local ops, `ApiReader` for GETs.
- **Vendor.Web**: same pattern with dual-backend support (ClientOpEngine
  → Order.Api, VendorApiClient → Vendor.Api).

### 1.6 Notifier integration
- `OrderNotifications` (5 types) + `VendorNotifications` (2 types)
  defined as `NotificationType` objects with channels and priority.
- Order.Api sends `Notifier.SendAsync(NewOrder, vendorId)` after order
  creation; sends `OrderAccepted/Ready/Delivered/Rejected` to customer
  on vendor callback.
- Vendor.Api sends `OrderReceived` to vendor on incoming webhook.

---

## 2. The Operation-Accounting Programming Model

The central insight from this session: what started as "use OpEngine in
controllers" evolved into a **complete programming model** — a way to
think about every state change at every layer of the stack.

### The model in one sentence

> Every meaningful state transition — backend or frontend, server or
> client, business logic or UI preference — is expressed as a
> double-entry accounting operation with typed parties, tags, analyzers,
> and interceptors.

### How it replaces traditional patterns

| Traditional | Operation-Accounting |
|---|---|
| `Service.DoSomething()` | `Entry.Create("something.do")` |
| `if (cond) throw` | `.Analyze(new PredicateAnalyzer(...))` |
| `middleware` | `TaggedInterceptor("tag", Pre/Post, ...)` |
| `event handler` | `IOperationInterpreter<TStore>` |
| `HttpClient.PostAsync()` | `HttpDispatchInterceptor` (tag: `client_dispatch`) |
| `setState(newValue)` | `StateBridgeInterceptor` → interpreter → store |
| `try/catch` | `OperationResult.Success/Failed` |
| `DTO` | `OperationEnvelope<T>` |

### The four layers

```
┌─────────────────────────────────────────────────────────┐
│ Layer 4 — Pages/Views                                    │
│ @inject AppStore → read state                            │
│ @inject ClientOpEngine → Entry.Create("x") → execute     │
│ No services. No manual HTTP. No setState.                │
└──────────────────────────┬──────────────────────────────┘
                           │ operations
┌──────────────────────────▼──────────────────────────────┐
│ Layer 3 — Client OpEngine                                │
│ ClientOpEngine.ExecuteAsync(op, payload)                  │
│ Pre: local analyzers (RequiredFieldAnalyzer)              │
│ Dispatch: HttpDispatchInterceptor → http.send entry       │
│ Post: StateBridgeInterceptor → interpreters → AppStore    │
└──────────────────────────┬──────────────────────────────┘
                           │ HTTP (OperationEnvelope<T>)
┌──────────────────────────▼──────────────────────────────┐
│ Layer 2 — Server OpEngine                                │
│ Entry.Create("order.create")                              │
│ Pre: interceptors (QuotaCheck, ScheduleGate)              │
│ Execute: DB mutations inside ctx => { ... }               │
│ Post: interceptors (AuditLogger, Notifier)                │
│ Wire: OperationEnvelope<T> → client                       │
└──────────────────────────┬──────────────────────────────┘
                           │ webhooks / callbacks
┌──────────────────────────▼──────────────────────────────┐
│ Layer 1 — Inter-service (Vendor.Api ↔ Order.Api)         │
│ Webhook = POST with OperationEnvelope payload             │
│ Callback = POST with action string                        │
│ Timeout = background service executing order.timeout entry│
└─────────────────────────────────────────────────────────┘
```

---

## 3. Key architectural decisions

### 3.1 Shared backend for the same domain
Order.Api serves both customer and vendor *reads* because they query
the same ledger. Vendor.Api is a separate process for vendor *mutations*
(accept/reject/deliver) with its own database and interceptors. The
separation is by **responsibility and failure domain**, not by UI.

### 3.2 No service classes
The traditional `CartService`, `AuthStateService`, `UiPreferences` are
replaced by:
- **Operation factories** (`ClientOps.CartAdd(...)`)
- **Interpreters** (`CartInterpreter`, `AuthInterpreter`, `UiInterpreter`)
- **AppStore** (single state container, updated only by interpreters)

### 3.3 Reads don't need accounting
GET requests use `ApiReader` (a thin HTTP wrapper) because reads don't
change state. Only mutations go through OpEngine.

### 3.4 Reporting operations
View counts, read receipts, presence — modeled as one-sided entries
with balanced amounts (1:1). Value comes from operation *count*, not
from amounts. Reporting interceptors watch specific types and maintain
aggregates.

---

## 4. What the user identified that we missed

1. **Vendor.Web Login was calling non-existent auth endpoints** — I
   invented `/api/auth/request-otp` instead of reading the actual
   `/api/auth/sms/request`. Lesson: always read existing code first.

2. **All template icons were unprofessional emojis** — replaced with
   monochrome SVG line icons system-wide.

3. **The accounting model wasn't applied anywhere** — despite the
   libraries being fully built, the app code bypassed them entirely.
   Required a full audit + conversion.

4. **Client libraries existed but weren't used** — `ClientOpEngine`,
   `HttpDispatchInterceptor`, `StateBridgeInterceptor` were built but
   Order.Web and Vendor.Web used raw `HttpClient` wrappers instead.

5. **Inter-service communication used raw HTTP** instead of the
   platform's own `Notifier` + `IRealtimeTransport` infrastructure.

---

## 5. Current state (end of Session 2)

### Project count: 40 projects in ACommerce.Platform.sln

### Applications
| App | Port | Status |
|---|---|---|
| Order.Api | 5101 | ✅ Tested (auth, orders, offers, messages, notifications) |
| Vendor.Api | 5201 | ✅ Tested (settings, schedule, order lifecycle, interceptors) |
| Order.Web | 5701 | ✅ Builds, ClientOpEngine wired |
| Vendor.Web | 5801 | ✅ Builds, ClientOpEngine + VendorApiClient wired |
| Ashare.Api | 5500 | ✅ All mutations via OpEngine |
| Ashare.Web | — | Functional (not yet migrated to ClientOpEngine) |

### OpEngine coverage
- **Backend**: 100% — every DB mutation across all 3 APIs
- **Frontend**: Order.Web 100%, Vendor.Web ~80%, Ashare.Web 0% (legacy)
