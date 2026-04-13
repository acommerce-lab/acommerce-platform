# ACommerce Platform — Architecture

This repository hosts **server accounting libraries**, **client libraries**,
and a **widgets + templates cascade** that together let a small team (or a
single AI agent) produce a production-ready multi-vendor e-commerce
application in days.

The core idea: every state change is a double-entry accounting operation.
See `MODEL.md` for the full definition. See `LIBRARY-ANATOMY.md` for how
domain libraries are structured. See `ROADMAP.md` for what's done and
what's next.

---

## The four architectural layers

```
┌──────────────────────────────────────────────────────────────────┐
│  Layer 4 — Apps                                                   │
│  Order.Api (5101), Vendor.Api (5201), Ashare.Api (5500)          │
│  Order.Web (5701), Vendor.Web (5801), Ashare.Web                  │
│  Domain entities, HTTP controllers, Blazor pages, brand CSS       │
└──────────────────────────────────────────────────────────────────┘
                               ▲
┌──────────────────────────────────────────────────────────────────┐
│  Layer 3 — Client Libraries                                       │
│  ACommerce.Client.Operations  (ClientOpEngine, dispatchers)       │
│  ACommerce.Client.Http        (HttpDispatcher, route registry)    │
│  ACommerce.Client.StateBridge (interpreters, state applier)       │
│  AppStore + ClientOps + Interpreters per app                      │
└──────────────────────────────────────────────────────────────────┘
                               ▲
┌──────────────────────────────────────────────────────────────────┐
│  Layer 2 — Domain / Operations                                    │
│  Auth, Payments, Notifications, Subscriptions, Files, Favorites   │
│  Each follows the three-layer anatomy (see LIBRARY-ANATOMY.md):   │
│    L1: pure entries + analyzers                                   │
│    L2: provider contracts                                         │
│    L3: injectable interceptors                                    │
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

Every layer reads **downward** only.

---

## Layer 1 — Core libraries

| Library | Purpose |
|---|---|
| `ACommerce.SharedKernel.Abstractions` | `IBaseEntity`, `IBaseAsyncRepository<T>`, `IRepositoryFactory`, `EntityDiscoveryRegistry`. Every entity implements `IBaseEntity`. Every repository implements the same async interface. |
| `ACommerce.SharedKernel.Infrastructure.EFCores` | EF Core adapter. Auto-discovers registered entities. Exposes `AddACommerceSQLite()`, `AddACommerceSqlServer()`, `AddACommerceInMemoryDatabase()`. |
| `ACommerce.OperationEngine` | The accounting kernel: `Operation`, `Party`, `Tag`, `OpEngine`, `OperationBuilder`, `AccountingBuilder` (`Entry.Create`), built-in analyzers. |
| `ACommerce.OperationEngine.Wire` | `OperationEnvelope<T>`, `OperationDescriptor`, `OperationError`. The wire format for every HTTP response. |
| `ACommerce.OperationEngine.Interceptors` | `IOperationInterceptor`, `OperationInterceptorRegistry`, `PredicateInterceptor`. Cross-cutting Pre/Post interceptors. |

---

## Layer 2 — Domain libraries

### Authentication & authorization
- `ACommerce.Authentication.Operations` — `AuthConfig`, `AuthService`, `IAuthenticator`, `ITokenIssuer`, `ITokenValidator`
- `ACommerce.Authentication.Providers.Token` — JWT token issuer/validator
- `ACommerce.Authentication.TwoFactor.Operations` — `TwoFactorService`, `ITwoFactorChannel`
- `ACommerce.Authentication.TwoFactor.Providers.Sms` — mock SMS (logs OTP)
- `ACommerce.Authentication.TwoFactor.Providers.Email` — SMTP channel
- `ACommerce.Authentication.TwoFactor.Providers.Nafath` — Saudi ID channel
- `ACommerce.Permissions.Operations` — permission interceptor

### Payments
- `ACommerce.Payments.Operations` — `IPaymentGateway`
- `ACommerce.Payments.Providers.Noon` — Noon gateway (Saudi market)

### Messaging & realtime
- `ACommerce.Realtime.Operations` — `IRealtimeTransport`
- `ACommerce.Realtime.Providers.InMemory` — in-process fanout
- `ACommerce.Notification.Operations` — `INotificationChannel`
- `ACommerce.Notification.Providers.InApp` — DB + realtime delivery
- `ACommerce.Notification.Providers.Firebase` — FCM push

### Subscriptions
- `ACommerce.Subscriptions.Operations` — `QuotaInterceptor`, `QuotaConsumptionInterceptor` (gate operations tagged `quota_check`)

### Files
- `ACommerce.Files.Abstractions` — `IStorageProvider`
- `ACommerce.Files.Operations` — `FileService`
- `ACommerce.Files.Storage.Local` / `.AliyunOSS` / `.GoogleCloud`

### Utilities
- `ACommerce.Favorites.Operations` — generic favourites
- `ACommerce.Translations.Operations` — translation storage

---

## Layer 3 — Client libraries

- `ACommerce.Client.Operations` — `ClientOpEngine`, `IOperationDispatcher`
- `ACommerce.Client.Http` — `HttpDispatcher`, `HttpRouteRegistry`
- `ACommerce.Client.StateBridge` — `IOperationInterpreter<TStore>`, `IStateApplier`, `OperationInterpreterRegistry<TStore>`

---

## Layer 4 — Widgets + Templates

- **`ACommerce.Widgets`** — atomic primitives + `:root` CSS variables + Bootstrap 5 compatibility layer
- **`ACommerce.Templates.Shared`** — role-agnostic composites (AcLoginPage, AcChatPage, AcNotificationsPage, AcProfilePage, AcSettingsPage, AcBottomNav, etc.)
- **`ACommerce.Templates.Customer.Commerce`** — commerce-shaped composites (AcCatalogHome, AcCartPage, AcCheckoutPage, AcOrderDetailsPage, etc.)
- **`ACommerce.Templates.Commerce`** — legacy composites (AcShell, AcAuthPanel, AcProductCard, AcPlanCard, AcChatBubble)

---

## Layer 4 — Applications

| App | Port | Description |
|---|---|---|
| Order.Api | 5101 | Customer-facing: offers, orders, messages, notifications, favorites |
| Order.Web | 5701 | Blazor frontend for customers (orange brand) |
| Vendor.Api | 5201 | Vendor-facing: order accept/reject/deliver, settings, schedule |
| Vendor.Web | 5801 | Blazor frontend for vendors (teal brand) |
| Ashare.Api | 5500 | Property classifieds: listings, bookings, subscriptions |
| Ashare.Web | — | Blazor frontend for classifieds (purple brand) |

---

## Getting started

```bash
dotnet build ACommerce.Platform.sln

# Run Order demo
dotnet run --project Apps/Order.Api &
dotnet run --project Apps/Order.Web
# API → http://localhost:5101/swagger
# Web → http://localhost:5701
```

## Documentation

1. **`MODEL.md`** — the Operation-Accounting Model definition
2. **`LIBRARY-ANATOMY.md`** — three-layer pattern for domain libraries
3. **`BUILDING-A-BACKEND.md`** — step-by-step recipe for a new backend
4. **`BUILDING-A-FRONTEND.md`** — step-by-step recipe for a new Blazor frontend
5. **`ROADMAP.md`** — what's done, what's next, modification plan
