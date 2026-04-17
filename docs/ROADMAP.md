# Roadmap — modification plan

## Current state (end of Session 2)

### Applications: 6 (40 projects total)

| App | Port | OpEngine Backend | OpEngine Frontend |
|---|---|---|---|
| Order.Api | 5101 | 100% | — |
| Order.Web | 5701 | — | 100% (ClientOpEngine) |
| Vendor.Api | 5201 | 100% | — |
| Vendor.Web | 5801 | — | ~80% |
| Ashare.Api | 5500 | 100% | — |
| Ashare.Web | — | — | 0% (legacy services) |

### What was accomplished in Sessions 1–2

- Core libraries built: OperationEngine, Wire, Interceptors, SharedKernel
- Client libraries built: Client.Operations, Client.Http, Client.StateBridge
- Widget cascade: 10 atomic widgets + Bootstrap compat layer (664 lines CSS)
- Templates: Shared (14 components) + Customer.Commerce (10 components)
- AcIcon system: 35+ inline SVG line icons replacing emoji
- Vendor.Api microservice with 3 interceptors + timeout background service
- 100% OpEngine coverage across all 3 backends
- Order.Web fully migrated to ClientOpEngine + AppStore + Interpreters
- Inter-service webhooks (Order.Api ↔ Vendor.Api)
- Notifier integration (7 notification types)
- Professional typography (Tajawal 14px) + neutral-first theme + dark mode

---

## Phase 0 — Core model enhancements

Modifications to the OperationEngine and supporting libraries to formalize
concepts discussed in the model definition.

### 0.1 ProviderContract concept in OperationBuilder

Add `.Requires<T>()` to the builder and `ctx.Provider<T>()` to the context.
This makes the dependency declaration explicit in the entry definition
rather than hidden inside the execute body.

**Files to modify:**
- `libs/backend/core/ACommerce.OperationEngine/Core/OperationBuilder.cs`
  — add `Requires<T>()` method that records the contract type
- `libs/backend/core/ACommerce.OperationEngine/Core/Operation.cs`
  — add `RequiredContracts` list
- `libs/backend/core/ACommerce.OperationEngine/Core/OperationContext.cs`
  — add `Provider<T>()` convenience method (resolves from Services)
- `libs/backend/core/ACommerce.OperationEngine/Core/OperationEngine.cs`
  — validate that all required contracts are registered before Execute
- `libs/backend/core/ACommerce.OperationEngine/Patterns/AccountingPattern.cs`
  — expose `Requires<T>()` on AccountingBuilder

**Does NOT change**: existing code continues to work. `Requires<T>()` is
optional but recommended for new libraries.

### 0.2 Entry journaling interceptor

A built-in Post interceptor that persists the entry itself (type, parties,
tags, result, timestamp) to a journal table. This enables:
- Audit trail without custom interceptors per app
- Account queries (all parties where identity = X)
- Replay and debugging

**Files to create:**
- `libs/backend/core/ACommerce.OperationEngine.Interceptors/JournalInterceptor.cs`
- `libs/backend/core/ACommerce.SharedKernel.Abstractions/Entities/JournalEntry.cs`

**Registration**: opt-in via `services.AddOperationJournal()`.

### 0.3 Account query API

Given the journal from 0.2, add a query layer:
- `IAccountQuery.GetPartiesAsync(identity, dateRange?, tags?)`
- `IAccountQuery.GetBalanceAsync(identity, valueAggregator?)`

This makes "accounts" (half-entries) a first-class queryable concept.

**Files to create:**
- `libs/backend/core/ACommerce.OperationEngine/Accounts/IAccountQuery.cs`
- `libs/backend/core/ACommerce.OperationEngine/Accounts/JournalAccountQuery.cs`

---

## Phase 1 — Domain library restructuring

Restructure existing domain libraries to follow the three-layer anatomy
defined in `LIBRARY-ANATOMY.md`.

### 1.1 Demo verification service (replaces Auth.Api idea)

The problem: Vendor.Web calls Order.Api for OTP verification. Each app
should have its own verification provider without depending on another
app's backend.

**Solution**: NOT a standalone Auth.Api service. Instead:
- The `ACommerce.Authentication.TwoFactor.Providers.Sms` library already
  contains a `LoggingSmsSender` that prints OTP to logs
- Each app registers this provider in its own DI container
- No inter-service dependency for auth

**What to verify**: that Order.Api, Vendor.Api, and Ashare.Api each have
their own SMS 2FA provider registered. If Vendor.Web currently calls
Order.Api for auth, rewire it to call Vendor.Api's own auth endpoints.

**Files to check/modify:**
- `Apps/Vendor.Web/Operations/VendorRoutes.cs` — auth routes should point
  to Vendor.Api, not Order.Api
- `Apps/Vendor.Api/Program.cs` — ensure auth + 2FA services are registered

### 1.2 Complete Ashare.Web ClientOpEngine migration

Ashare.Web still uses `AshareApiClient` + `AuthStateService` service
classes. Migrate to `AppStore` + `ClientOpEngine` +
`OperationInterpreterRegistry<AppStore>` following the Order.Web pattern.

**Files to create in `Apps/Ashare.Web/`:**
- `Operations/ClientOps.cs` — entry factories
- `Operations/AshareRoutes.cs` — HTTP route registry
- `Interpreters/AuthInterpreter.cs`
- `Interpreters/UiInterpreter.cs`
- `Store/AppStore.cs`
- `Store/AppStateApplier.cs`
- `Store/ApiReader.cs`

**Files to modify:**
- `Apps/Ashare.Web/Program.cs` — wire ClientOpEngine + interpreters
- All pages under `Components/Pages/` — inject AppStore + Engine

**Files to delete:**
- Legacy service classes (AshareApiClient, AuthStateService, etc.)

---

## Phase 2 — Operation-aware templates

Transform templates from passive (callback-based) to active
(operation-emitting). This is the key change that enables template
portability and zero-code-behind pages.

### 2.1 Template base infrastructure

**Files to create in `libs/frontend/ACommerce.Templates.Shared/`:**
- `Infrastructure/ITemplateEngine.cs` — interface that templates use to
  emit operations (wraps ClientOpEngine)
- `Infrastructure/ITemplateStore.cs` — interface that templates use to
  read state (wraps AppStore)
- `Infrastructure/TemplateBase.razor` — base component that injects
  engine + store via cascading parameters

### 2.2 Convert Shared templates to operation-aware

Each shared template gets two modes:
- **Callback mode** (backward compatible): `OnRequestOtp`, `OnSend`, etc.
- **Operation mode** (new): `Engine` + `Store` parameters, template emits
  operations internally

Templates to convert (priority order):
1. `AcLoginPage` — emits `auth.sms.request` and `auth.sms.verify`
2. `AcChatPage` — emits `message.send` and `conversation.mark_read`
3. `AcMessagesListPage` — emits `conversation.select`
4. `AcNotificationsPage` — emits `notification.read` and `notification.mark_all_read`
5. `AcProfilePage` — emits `auth.sign_out`
6. `AcSettingsPage` — emits `ui.set_theme` and `ui.set_language`

### 2.3 Convert Customer.Commerce templates to operation-aware

1. `AcCatalogHome` — emits `catalog.search`, `catalog.select_category`
2. `AcOfferDetailsPage` — emits `cart.add`, `favorite.toggle`
3. `AcCartPage` — emits `cart.set_quantity`, `cart.clear`, `cart.checkout`
4. `AcCheckoutPage` — emits `order.create`
5. `AcOrdersListPage` — reads from store
6. `AcOrderDetailsPage` — emits `order.cancel`
7. `AcFavoritesPage` — emits `favorite.toggle`

### 2.4 Widget-entity binding

Atomic widgets (`AcButton`, `AcInput`, `AcCard`) stay passive — they are
pure UI primitives with no business logic.

Composite templates (`AcLoginPage`, `AcCartPage`) become operation-aware —
they are bound to entry types and store slices.

The boundary: **if the component emits a state change, it's a template and
should be operation-aware. If it only renders, it's a widget and stays
passive.**

---

## Phase 3 — Merchant and Admin templates

### 3.1 ACommerce.Templates.Merchant.Commerce

Create merchant-facing templates using the operation-aware pattern:

| Template | Entries emitted |
|---|---|
| `AcVendorDashboard` | (read-only, uses ApiReader) |
| `AcVendorOrderCard` | `vendor-order.accept`, `vendor-order.reject`, `vendor-order.ready`, `vendor-order.deliver` |
| `AcVendorOfferForm` | `offer.create`, `offer.update` |
| `AcVendorSchedule` | `vendor.schedule.update` |
| `AcVendorSettings` | `vendor.settings.update` |

### 3.2 ACommerce.Templates.Admin

| Template | Entries emitted |
|---|---|
| `AcAdminUsersPage` | `admin.user.block`, `admin.user.unblock` |
| `AcAdminVendorsPage` | `admin.vendor.approve`, `admin.vendor.suspend` |
| `AcAdminOrdersPage` | (read-only) |
| `AcAdminMetricsPage` | (read-only, uses Account queries from Phase 0.3) |

---

## Phase 4 — Production hardening

### 4.1 Role-based auth middleware
Vendor endpoints require vendor JWT claims; customer endpoints require
customer claims. Implemented as a Pre interceptor on `tag("requires_role")`.

### 4.2 Rate limiting interceptor
Tag-based rate limiting: `tag("rate_limit", "10/min")`. Implemented as a
Pre interceptor using in-memory sliding window.

### 4.3 AppStore persistence
`ProtectedLocalStorage` adapter for AppStore so auth state survives page
reloads. Replace current in-memory AppStore with hybrid (memory + storage).

### 4.4 Real SMS provider
Twilio/Vonage adapter for `ITwoFactorChannel`. The `LoggingSmsSender`
stays as default; production swaps via configuration.

### 4.5 Redis realtime transport
Replace `InMemoryRealtimeTransport` with Redis pub/sub for multi-instance
deployments.

---

## Phase B — Dynamic Attributes (Ashare) + Legacy Migration

### B.1 Template + Snapshot model — DONE

- `DynamicAttribute` + `AttributeTemplate` + `DynamicAttributeHelper` في SharedKernel.
- `Category.AttributeTemplateJson` + `Listing.DynamicAttributesJson` في Ashare.
- 5 قوالب فئات في `AshareCategoryTemplates` (Residential, LookingForHousing,
  LookingForPartner, Administrative, Commercial).
- Widgets: `AcDynamicAttributeField`, `AcDynamicAttributesView`.

### B.2 SQLite dev schema drift guard — DONE

`SqliteSchemaGuard` يحسب بصمة SHA-256 من أسماء الجداول + الأعمدة + الأنواع،
ويعيد بناء ملف SQLite عند الاختلاف. مُدمج في Program.cs لـ 5 تطبيقات.

### B.3 Legacy migrator tool — DONE

`tools/AshareMigrator/` console app يقرأ من SQL Server الإنتاجي القديم ويكتب
إلى SQLite محلي بالصيغة الجديدة:

- Legacy/Target DbContexts مفصولان (cross-DB).
- Mappers: Category, User (+ Profile), Listing, Booking, Plan, Subscription.
- **مبدأ "لا حذف بيانات"**: أي مفتاح صفات قديم غير موجود في قالب الفئة الجديدة
  يُحفظ حرفياً في اللقطة كـ `DynamicAttribute` إضافي بنوع مُستنتَج.
- idempotent: يتخطى الصفوف الموجودة بنفس Id.
- يدعم `--truncate` لإعادة التنفيذ من الصفر.
- سلسلة الاتصال مع كلمة المرور تُطبع مُخفّاة.

### B.4 Parity run + visual comparison — NEXT

- تشغيل الترحيل على بيانات إنتاج عشير الحقيقية.
- توجيه `Ashare.Api` المحلي إلى ملف SQLite الناتج.
- مقارنة سلوك القراءة/العرض (قوائم، تفاصيل، فلاتر) مع النسخة القديمة.
- توثيق أي فجوات في جدول.

---

## Phase 5 — Future

### 5.1 Real map integration
Swap `AcMapSearchPage` CSS grid background for Leaflet.js + OpenStreetMap
tiles. Same UX pattern (pins → popup → bottom sheet).

### 5.2 Scaffold CLI
`dotnet new ac-app MyApp` that produces a fully-wired Blazor Web App with
`MyApp.Web` / `MyApp.Api` ready to run.

### 5.3 Formal model specification
Write the mathematical definition of OAM as described in `MODEL.md`
algebraic structure section. Target: workshop paper or technical report.

---

## Session history

### Session 1 (foundation)
- Core libraries, SharedKernel, EF wrapper
- OperationEngine + Wire + Interceptors
- Ashare.Api + Ashare.Web (property classifieds)
- Order.Api + Order.Web (cafe deals)
- Widgets cascade + Bootstrap compat layer
- Templates.Commerce + Templates.Shared (initial)
- Repository split from monorepo (114 → 38 projects)

### Session 2 (discipline enforcement)
- AcIcon system (35+ SVG line icons)
- Professional typography + neutral theme + dark mode
- Vendor.Api microservice (interceptors, timeout service)
- 100% OpEngine coverage across all backends
- Order.Web ClientOpEngine migration (zero service classes)
- Vendor.Web ClientOpEngine migration (~80%)
- Notifier integration (7 notification types)
- Inter-service webhooks
- SESSION-2-SUMMARY.md + NEXT-STEPS.md written

### Session 3 (model formalization + documentation)
- .NET 10.0.201 SDK installed and verified
- All 40 projects build successfully on net10.0
- MODEL.md — formal definition of the Operation-Accounting Model
- LIBRARY-ANATOMY.md — three-layer pattern for domain libraries
- ROADMAP.md — comprehensive modification plan
- CLAUDE.md — unified agent onboarding
- Documentation cleanup (removed obsolete split/test/restructuring plans)
