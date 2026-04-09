# ACommerce Platform — Architecture

The short version: this repository hosts a set of **server accounting
libraries**, **client libraries**, and a **widgets + templates cascade** that
together let a small team (or a single AI agent) produce a production-ready
multi-vendor e-commerce application in a few working days rather than a few
working months. The two current demo apps — Ashare (property classifieds) and
Order (cafe/restaurant deals) — were built on this stack to prove it end-to-end.

This document is the top-level map. For the deep-dive philosophy see
`ACCOUNTING-PHILOSOPHY.md`. For step-by-step recipes see `BUILDING-A-BACKEND.md`
and `BUILDING-A-FRONTEND.md`. For the AI-agent onboarding brief see
`AI-AGENT-ONBOARDING.md`.

---

## The three architectural layers

```
┌──────────────────────────────────────────────────────────────────┐
│  Layer 3 — Apps                                                   │
│  Ashare.Api, Ashare.Web, Order.Api, Order.Web, …              │
│  Domain entities, HTTP controllers, Blazor pages, brand CSS       │
└──────────────────────────────────────────────────────────────────┘
                               ▲
┌──────────────────────────────────────────────────────────────────┐
│  Layer 2 — Domain / Operations                                    │
│  Auth.Operations, Payments.Operations, Notification.Operations,   │
│  Subscriptions.Operations, Favorites.Operations, Files.Operations │
│  Each defines its Op types, analyzers, interceptors, providers    │
└──────────────────────────────────────────────────────────────────┘
                               ▲
┌──────────────────────────────────────────────────────────────────┐
│  Layer 1 — Core                                                   │
│  ACommerce.OperationEngine          (the accounting kernel)       │
│  ACommerce.OperationEngine.Wire     (OperationEnvelope<T>)        │
│  ACommerce.OperationEngine.Interceptors                           │
│  ACommerce.SharedKernel.Abstractions    (IBaseEntity, IRepo<T>)   │
│  ACommerce.SharedKernel.Infrastructure.EFCores  (EF wrapper)      │
└──────────────────────────────────────────────────────────────────┘
```

Every layer reads **downward** only. Apps never reach into the Core
directly — they go through domain operations.

---

## Layer 1 — Core libraries (the non-negotiables)

| Library | Purpose |
|---|---|
| `ACommerce.SharedKernel.Abstractions` | `IBaseEntity`, `IBaseAsyncRepository<T>`, `IRepositoryFactory`, `EntityDiscoveryRegistry`. Every entity in the platform implements `IBaseEntity`. Every repository implements the same async interface so the apps never know whether the backing store is SQLite, SQL Server, or in-memory. |
| `ACommerce.SharedKernel.Infrastructure.EFCores` | EF Core implementation of the abstractions. Picks up every registered entity automatically, builds an `ApplicationDbContext`, and exposes `AddACommerceSQLite()`, `AddACommerceSqlServer()`, `AddACommerceInMemoryDatabase()` extension methods. **This is the adapter that lets every app run on SQLite for the preview and swap to SQL Server for production with one config line.** |
| `ACommerce.OperationEngine` | The accounting kernel. An `Operation` is a strongly-typed value object with `Parties`, `Tags`, `Analyzers`, and an `Execute` delegate. The `OpEngine` executes an operation through a pipeline of interceptors, analyzers, and the actual execute body, wrapping the whole thing in an accounting entry. |
| `ACommerce.OperationEngine.Wire` | `OperationEnvelope<T>`, `OperationInfo`, `OperationError` — the wire format. Every HTTP controller method on the backend returns `OperationEnvelope<T>`, and the frontend clients deserialize into the same shape. **This is the contract that keeps the backend and the frontend honest.** |
| `ACommerce.OperationEngine.Interceptors` | Cross-cutting Pre/Post/Wrap interceptors. Used for subscription quota checks, permission checks, audit logging, anything that should run around operations of a certain tag or type. |

These five libraries are the ones **every** new app will need. If you spin
off a "platform-only" repository, these are the non-negotiable imports.

---

## Layer 2 — Domain operation libraries (pick the ones you need)

Each of these is a **thin**, **focused** library that defines:
- An abstraction (interface)
- One or more concrete providers
- Optional analyzers and interceptors
- Extension methods to register the whole bundle in DI

The apps compose their own feature set by picking the libraries they need.

### Authentication & authorization

- `ACommerce.Authentication.Operations` — `AuthConfig`, `AuthService`, `IPrincipal`, `IAuthenticator`, `ITokenIssuer`, `ITokenValidator`.
- `ACommerce.Authentication.Providers.Token` — JWT token issuer / validator you can plug straight into the above.
- `ACommerce.Authentication.TwoFactor.Operations` — `TwoFactorService`, `ITwoFactorChannel`, challenge records.
- `ACommerce.Authentication.TwoFactor.Providers.Sms` — mock SMS channel (logs the OTP). Replace with Twilio/Unifonic in production.
- `ACommerce.Authentication.TwoFactor.Providers.Email` — SMTP channel.
- `ACommerce.Authentication.TwoFactor.Providers.Nafath` — Nafath (Saudi ID) channel.
- `ACommerce.Permissions.Operations` — permission predicates + interceptor that evaluates them on operations tagged with a permission requirement.

### Payments

- `ACommerce.Payments.Operations` — `PaymentConfig`, `PaymentService`, `IPaymentGateway`.
- `ACommerce.Payments.Providers.Noon` — Noon payment gateway provider (Saudi market).

### Messaging & realtime

- `ACommerce.Realtime.Operations` — `IRealtimeTransport` abstraction.
- `ACommerce.Realtime.Providers.InMemory` — in-process fanout, fine for a single-server demo.
- `ACommerce.Notification.Operations` — `INotificationChannel` abstraction.
- `ACommerce.Notification.Providers.InApp` — stores notifications in the DB and delivers them over realtime.
- `ACommerce.Notification.Providers.Firebase` — FCM push notifications.

### Subscriptions (optional — Ashare uses them, Order doesn't)

- `ACommerce.Subscriptions.Operations` — `ISubscriptionProvider`, `QuotaInterceptor`, `QuotaConsumptionInterceptor`. The interceptors plug into `ACommerce.OperationEngine.Interceptors` and automatically gate operations tagged with `quota_check`.

### Files

- `ACommerce.Files.Abstractions` — `IStorageProvider`.
- `ACommerce.Files.Operations` — `FileService` on top of the abstraction.
- `ACommerce.Files.Storage.Local` — disk storage.
- `ACommerce.Files.Storage.AliyunOSS` — Alibaba Cloud OSS.
- `ACommerce.Files.Storage.GoogleCloud` — GCS.

### Utilities

- `ACommerce.Favorites.Operations` — generic favourites.
- `ACommerce.Translations.Operations` — translation storage / retrieval.

---

## Layer 2 on the frontend — Client libraries

These are the frontend-side counterparts that talk to the backend operation
endpoints and return `OperationEnvelope<T>`:

- `ACommerce.Client.Operations` — the base abstractions.
- `ACommerce.Client.Http` — `HttpDispatcher` that speaks `OperationEnvelope<T>`.
- `ACommerce.Client.StateBridge` — optional reactive bridge.
- `ACommerce.Client.Domain.*` — per-domain clients (Auth, Listings, …).

Most apps only need `ACommerce.OperationEngine.Wire` (for the envelope
type) and a hand-rolled thin `HttpClient` wrapper, as Ashare.Web and
Order.Web demonstrate. The client libraries are there if you want a
richer reactive layer.

---

## Layer 2 on the frontend — Widgets + Templates

This is the styling cascade.

- **`ACommerce.Widgets`** (`libs/frontend/ACommerce.Widgets/wwwroot/widgets.css`)  
  Atomic primitives + `:root` variables + **Bootstrap compatibility layer**. Defines `.ac-btn`, `.ac-card`, `.ac-alert`, `.ac-input`, etc. **AND** `.btn`, `.card`, `.alert`, `.form-control`, `.row`, `.col-*`, `.text-muted`, `.bg-primary` — the full Bootstrap 5 vocabulary is translated to the same `--ac-*` variables. Any Razor page using either set of class names gets themed automatically.

- **`ACommerce.Templates.Commerce`** (`libs/frontend/ACommerce.Templates.Commerce/wwwroot/templates.css`)  
  Composite templates built out of widgets primitives: `AcShell` (top-nav layout), `AcAuthPanel` (login card), `AcProductCard`, `AcPlanCard`, `AcPageHeader`, `AcChatBubble`. Each is a `.razor` component that assembles widgets into a useful shape.

**The cascade is the whole trick.** An app overrides `--ac-primary`, `--ac-secondary`, font, radii once in its own `wwwroot/app.css`. Every widget, every template, every Bootstrap-class-using page re-paints with the new brand. Ashare is purple, Order is orange — **same codebase, different `:root`**.

For the honest answer to "why only one template project?" and the roadmap
for the templates we still need, see `TEMPLATES-ROADMAP.md`.

---

## Layer 3 — The two demo apps

### Ashare (property classifieds)

- `Apps/Ashare.Api` — OpEngine + SQLite + JWT + SMS 2FA + Subscriptions (with `QuotaInterceptor`) + Listings + Bookings + Payments + Messages + Notifications.
- `Apps/Ashare.Web` — Blazor Web App using widgets + templates + a purple Ashare brand in `wwwroot/app.css`.

### Order (cafe/restaurant deals)

- `Apps/Order.Api` — OpEngine + SQLite + JWT + SMS 2FA + Offers + Orders (with curbside pickup + cash change calculator, no delivery/payment provider) + Messages + Notifications + Favorites.
- `Apps/Order.Web` — Blazor Web App with orange Order brand + shell + dark theme + bilingual (ar/en) settings.

Both apps are **leaf nodes** — they import core + the operation libraries
they need, write their own entities, and expose HTTP endpoints that all
return `OperationEnvelope<T>`.

---

## Getting started

```bash
# Clone
git clone <repo>
cd acommerce.libraries

# Run Order (the newest, cleanest demo)
bash Apps/Order.Web/run-local.sh
#   API  -> http://localhost:5101/swagger
#   WEB  -> http://localhost:5701
#   Login with +966500000001, OTP printed in /tmp/order-api.log

# Or run Ashare
# (API on 5500, Web on 5600, same pattern — see Apps/Ashare.Api/README)
```

For new projects, read (in order):
1. `ACCOUNTING-PHILOSOPHY.md` — the kernel's mental model.
2. `BUILDING-A-BACKEND.md` — how to add an entity, a controller, an operation.
3. `BUILDING-A-FRONTEND.md` — how to wire widgets + templates + a brand.
4. `TEMPLATES-ROADMAP.md` — what's missing and what to build next.
