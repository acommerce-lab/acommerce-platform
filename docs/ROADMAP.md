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

## Execution strategy — horizontal slicing

Starting with the Ashare-design import (session 5+), we deliver one
**page end-to-end** per session (widgets → template → app page) rather
than completing an entire layer at once.

Each slice:
1. Adds only the atomic widgets the page needs to `libs/frontend/ACommerce.Widgets`.
2. Adds the page's template to the right `Templates.*` library,
   operation-aware (emits entries, reads from `ITemplateStore`).
3. Wires the page into the new `Apps/Ashare.V2` consumer app with a
   minimal backend endpoint returning an `OperationEnvelope`.
4. Extends `scripts/widget-contracts.json`, adds a `/catalog` entry,
   and runs `verify-page-structure / verify-css / verify-design-tokens /
   verify-widget-contracts` before committing.

Slice order (first pass):

| # | Slice | New widgets | New template | Ashare.V2 page |
|---|---|---|---|---|
| 1 | Home | AcSearchBox, AcCategoryTile, AcSpaceCard, AcSectionHeader, AcHScroll | AcMarketplaceHomePage | `/` |
| 2 | Explore | AcFilterChip, AcSortMenu, AcViewToggle, AcBottomSheet | AcListingExplorePage | `/explore` |
| 3 | Listing details | AcGallery, AcStickyActionBar, AcRatingStars | AcListingDetailsPage | `/space/{id}` |
| 4 | Booking wizard | AcProgressSteps | AcBookingWizardPage | `/book/{id}` |
| 5 | Version gate | (none — re-uses existing) | AcVersionGate | app-level guard |
| 6 | Auth (Nafath + SMS) | — | AcNafathLoginPage | `/login` |
| 7 | Complaints, Legal, Profile | — | AcComplaintsPage, AcLegalPageView | `/help/*`, `/me` |

## Phase 0 — Core model enhancements — DONE

تحسينات OperationEngine لإضفاء الصفة الرسمية على مفاهيم النموذج.

### 0.1 ProviderContract concept — DONE

- `.Requires<T>()` على `OperationBuilder` و`AccountingBuilder`.
- `ctx.Provider<T>()` اختصار واضح الاسم في `OperationContext`.
- `RequiredContracts` في `Operation` + تحقّق `OpEngine` قبل Execute
  يُفشل العملية إذا لم يُسجَّل المزوّد في DI.

الملفات: `Core/OperationBuilder.cs`, `Core/Operation.cs`,
`Core/OperationContext.cs`, `Core/OperationEngine.cs`,
`Patterns/AccountingPattern.cs`.

### 0.2 Entry journaling interceptor — DONE

- `JournalInterceptor` (Post) يحفظ كل عملية غير مختومة (`sealed=true`
  يُستثنى) في جدول `journal_entries`.
- الحقول: النوع، الحالة، النجاح، Timestamp، `PartiesJson`, `TagsJson`,
  ParentOperationId.
- مُنشأ ككتبة مستقلة `ACommerce.OperationEngine.Journal`.

Opt-in عبر `services.AddOperationJournal()`.

### 0.3 Account query API — DONE

- `IAccountQuery.GetPartiesAsync(identity, dateRange?, tags?)`
- `IAccountQuery.GetBalanceAsync(identity, valueAggregator?)`
- `JournalAccountQuery` فوق `IBaseAsyncRepository<JournalEntry>`.
- مسجَّل تلقائياً ضمن `AddOperationJournal()`.

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

## Phase B — Dynamic Attributes (Ashare) + Production Data Integration

### B.1 Template + Snapshot model — DONE

- `DynamicAttribute` + `AttributeTemplate` + `DynamicAttributeHelper` في SharedKernel.
- `Category.AttributeTemplateJson` + `Listing.DynamicAttributesJson` في Ashare.
- 5 قوالب فئات في `AshareCategoryTemplates` (Residential, LookingForHousing,
  LookingForPartner, Administrative, Commercial).
- Widgets: `AcDynamicAttributeField`, `AcDynamicAttributesView`.
- راجع `docs/DYNAMIC-ATTRIBUTES.md` للتفاصيل.

### B.2 SQLite dev schema drift guard — DONE

`SqliteSchemaGuard` يحسب بصمة SHA-256 من أسماء الجداول + الأعمدة + الأنواع،
ويعيد بناء ملف SQLite عند الاختلاف. مُدمج في Program.cs لـ 5 تطبيقات.

### B.3 Legacy migrator tool — DEPRECATED

> **مُهجور**: استُبدل بـ B.4 (السيدر يجلب من API مباشرةً).
> الكود لا يزال في `tools/_archive/AshareMigrator/` للحالات التي لا يتوفر
> فيها API إنتاجي (مثل الترحيل من SQL Server مباشرةً).

### B.4 Production API backfill via seeder — DONE

`AshareSeeder.SeedListingsFromProductionAsync` يجلب العروض من
`https://api.ashare.sa/api/listings` عند كل تشغيل:

- يتعامل مع الاستجابة كمصفوفة JSON خام (لا OperationEnvelope).
- `images` مصفوفة أصلية → CSV.
- `attributes` كائن أصلي → لقطة DynamicAttribute مع قالب الفئة.
- `status` نصي ("Active") → enum رقمي.
- `vendorId` → `OwnerId` مع إنشاء مستخدمين بديلين.
- **مبدأ "لا بتر"**: المفاتيح غير الموجودة في القالب تُحفظ كصفات إضافية.
- عند فشل الاتصال → يعود لبيانات البذر المحلية.

### B.5 Missing items — legacy Ashare mobile features to carry over

Items found in `acommerce-lab/ACommerce.Libraries → Apps/Ashare.*` that
don't exist yet on the new platform:

| Item | Old location | Needed here |
|---|---|---|
| **Version gate** (force-update screen) | `Ashare.App/Components/AppStartup.razor` + `IAppVersionService` + `VersionCheckService` | `AcVersionGate` template + `app.version.check` entry + `IAppVersionChannel` provider contract |
| **Conditional shell** (hide navbar/bottom-nav on auth + full-screen routes) | `Ashare.Shared/Components/Layout/MainLayout.razor` | `AppStore.Ui.HideChromeOn: string[]` + `AcConditionalShell` template |
| **LegalPageView** (`/legal/{key}` iframe + retry) | `Ashare.Shared/Components/Pages/LegalPageView.razor` | `AcLegalPageView` template + `legal.fetch` entry |
| **Tracking consent** (once-per-user) | `ITrackingConsentService` | `tracking.consent.grant` / `.revoke` entries |
| **Listing drafts** (save-before-auth) | `PendingListingService` | `listing.draft.save` / `.resume` |
| **Complaints flow** | `Complaints.razor`, `ComplaintDetails.razor` | `AcComplaintsPage` + `complaint.file`, `complaint.reply` |
| **Cultural patterns (5)** | `ashare.css` (`.ashare-pattern-sadu/asiri/najdi/roshan/gypsum`) | SVG stroke-only patterns inherited via `currentColor` |
| **Splash asset** | `Ashare.App/Resources/Splash/splash.svg` | moved to `Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web/wwwroot/` |

### B.6 Template adaptation log

القوالب تتكيّف مع بيانات الإنتاج الحقيقية بدلاً من فرض شكل جديد:

| تاريخ | تغيير | سبب |
|---|---|---|
| 2026-04-17 | `amenities` → `features` | الإنتاج يستخدم `features` |
| 2026-04-17 | إضافة `requires_license`, `has_owner_license` | حقول يستخدمها صاحب المصلحة |

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

### Session 6 — Ashare V2 Migration: Pair A (Bootstrap)
- Branch: `claude/migrate-pair-v2-BKhe8`
- **Backend (Ashare.V2.Api)**:
  - تسجيل 19 كيان في `EntityDiscoveryRegistry`
  - إضافة Serilog + Swagger (v2 doc) + Health checks (`/healthz` + `/health` envelope)
  - `GlobalExceptionMiddleware` → `OperationEnvelope<object>` عند الاستثناءات
  - سكربتات فحص خلفيّة: `verify-backend-envelope.sh`, `verify-backend-mutations.sh`, `verify-backend-entries.sh`
  - اختبارات تكاملية: `tests/Ashare.V2.Api.Tests/HealthTests.cs` (3 اختبارات)
- **Frontend (Ashare.V2.Web)**:
  - تحديث ألوان العلامة التجارية في `app.css`: تيل داكن (#345454) + خلفية دافئة (#FEE8D6)
  - إصلاح تحذيرات البناء (CS8602 في ProfileEdit + RZ10012 في AcVersionGate)
  - صورة الشعار الأوّلية في `wwwroot/images/ashare-logo.png`
- **Build**: 0 warnings, 0 errors — frontend + backend + tests
- **Verify scripts**: envelope ✓, mutations ✓, entries ✓, page-structure ✓, css ✓, widget-contracts ✓, design-tokens ✓

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

### Session 4 (dynamic attributes + production data integration)
- Phase B Dynamic Attributes: Template + Snapshot in SharedKernel
- SqliteSchemaGuard: SHA-256 fingerprint auto-rebuild for dev DBs
- AshareMigrator (built then deprecated — replaced by seeder approach)
- AshareSeeder fetches real listings from api.ashare.sa at startup
- Template adaptation: amenities→features, +requires_license, +has_owner_license
- Principle established: "adapt to real data, never bend it"
- DYNAMIC-ATTRIBUTES.md, SEEDING.md expansion, RUNTIME-FINDINGS.md additions
