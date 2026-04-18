# خطّة ترحيل واجهة عشير القديمة إلى V2

**المصدر**: `/tmp/ACommerce.Libraries/Apps/Ashare.{Web,App,Shared,Admin}/`
**الوجهة**: `Apps/Ashare.V2.Web` (+ `Apps/Ashare.V2.App` MAUI لاحقاً + `Apps/Ashare.V2.Admin`)
**المراجع الداخليّة**:
- جرد المصدر: `docs/ASHARE-LEGACY-FRONTEND-INVENTORY.md`
- الاستشهادات المنهجيّة: `docs/ASHARE-METHODOLOGY-CITATIONS.md`
- نمط V2 في المستودع نفسه: `Apps/Ashare.V2/*` (مرجع تطبيقيّ للمنهجيّة لا مصدر)

**نطاق الخطّة**: كامل الواجهة القديمة — 25 صفحة في `Ashare.Shared` + shell الـ Web + shell الـ MAUI (مرحلة لاحقة) + 13 صفحة في `Ashare.Admin`.

---

## 0. المبادئ الحاكمة (مختصر قانونيّ)

كلّ مرحلة تحت هذه الخطّة ملزَمة بـ:

1. **القانون 1 (CLAUDE.md:41-53)**: لا طفرة حالة من الصفحة عبر خدمة مباشرة — كلّ متغيّر حالة يُبنى كـ `Entry.Create(...).From().To().Tag().Analyze().Execute(...).Build()` ويُرسَل عبر `ClientOpEngine` + `HttpDispatcher`.
2. **القانون 2 (CLAUDE.md:56-58)**: كلّ استجابة خادم = `OperationEnvelope<T>` — الصفحة تقرأ `envelope.Entity` لا JSON خام.
3. **القانون 4 (CLAUDE.md:68-69)**: فئات مرئيّة فقط عبر widgets (`Ac*`) أو Bootstrap المرخَّصة — لا ألوان hex في `.razor` ولا `bi bi-*` خامّة.
4. **القانون 5 (CLAUDE.md:71-74)**: أيّ تحميل بيانات يعتمد على الـ Auth يُنفَّذ في `OnAfterRenderAsync(firstRender: true)` بعد `await Auth.EnsureRestoredAsync()`.
5. **قواعد الترحيل الخمس (ASHARE-PAGE-MIGRATION.md:11-19)**: لا `<button>/<input>/.btn/.card` خام، لا `<i class="bi bi-*">`، لا ألوان hex في `.razor`، لا `AshareApiService`، كلّ استجابة `OperationEnvelope<T>`.

**خطّ التحقّق الإلزاميّ بعد كلّ مرحلة** (ASHARE-PAGE-MIGRATION.md:121-145, VERIFICATION-LAYERS.md):
```
scripts/verify-page-structure.sh   # Layer 1 — HTML/Bootstrap خام → 0
scripts/verify-css.sh              # Layer 2 — classes موجودة في CSS → 0
scripts/verify-widget-contracts.sh # Layer 3 — CSS_CLASSES header لكلّ widget
scripts/verify-design-tokens.sh    # Layer 3 — سلالم الخط/التباعد
scripts/verify-design-quality.sh   # Layer 4 — hex palette ≤60, no raw colours
scripts/verify-runtime.sh          # Layer 6 — Playwright + spatial-contracts.json
```
أيّ مخالفة = تُحلَّل وتُصلَح، لا تُتجاهَل.

---

## 1. المراحل

### المرحلة 0 — الأساس والهياكل (Foundations & Shells)

**النطاق**:
- إنشاء `Apps/Ashare.V2.Web` (Blazor Server) كـ shell فارغ.
- تسجيل `ClientOpEngine` + `HttpDispatcher` + `OperationInterpreterRegistry` في `Program.cs` (ASHARE-V2-METHODOLOGY.md:15-24).
- تركيب حزمة الثقافة عبر `AddBlazorCultureStack()` + `BrowserCultureProbe.InitAsync()` في `OnAfterRenderAsync` الخاصّ بـ `MainLayout` (CULTURE.md:54-62).
- تسجيل SDK عملاء ACommerce + Token management (نفس ما في Web shell القديم: `ITokenStorage`, `TokenManager`, `ScopedTokenProvider`).

**المصادر المرجعيّة من القديم**: `/tmp/ACommerce.Libraries/Apps/Ashare.Web/Program.cs` (69 سطراً).

**الاستشهادات المنهجيّة**:
- `ASHARE-METHODOLOGY-CITATIONS.md § BUILDING-A-FRONTEND.md` — ترتيب تسجيل الخدمات.
- `ASHARE-METHODOLOGY-CITATIONS.md § ASHARE-V2-METHODOLOGY.md` — نمط ClientOpEngine + HttpDispatcher.
- `ASHARE-METHODOLOGY-CITATIONS.md § CULTURE.md` — حزمة BlazorCultureStack.

**معايير القبول**:
- `dotnet build Apps/Ashare.V2.Web/Ashare.V2.Web.csproj` يُنهى بدون تحذيرات.
- `curl https://localhost:5001/health` → 200 + envelope.
- `scripts/verify-page-structure.sh Apps/Ashare.V2.Web` → 0 violations على الصفحة الفارغة.

**Definition of Done**: shell فارغ يقلع + الثقافة تُحمَّل + `ClientOpEngine` جاهز لاستقبال عمليّات.

**Commit**: `feat(ashare-v2): bootstrap Web shell with ClientOpEngine + culture stack`.

### المرحلة 1 — التخطيط (Layout) ونظام الألوان (Theme)

**النطاق**:
- `MainLayout.razor` (header + bottom-nav + body) يُعاد بناؤه من widgets فقط.
- Header: `<AcAppBar>` مع شعار `/images/ashare-logo.png` + `<AcIconButton Icon="menu" />` للقائمة.
- Bottom-nav: `<AcBottomNav>` بخمس عُقَد (Home / Explore / Chats / Bookings / Profile) بأيقونات `AcIcon`.
- Body: `<div class="acs-page">` حصراً (STYLING-METHODOLOGY.md:82-98).
- ملفّ `wwwroot/app.css` يحمل `:root` overrides لعلامة عشير فقط (BUILDING-A-FRONTEND.md:176-208):
  ```css
  :root {
    --ac-primary:       #345454;
    --ac-primary-dark:  #263F3F;
    --ac-primary-light: #4A6B6B;
    --ac-secondary:     #F4844C;
    --ac-bg:            #FEE8D6;
    --ac-surface:       #FFFFFF;
  }
  ```
  (مستنسَخ من `AddACommerceCustomerTemplate` في `Ashare.App/MauiProgram.cs:63-74`).
- ترتيب الـ cascade في `App.razor`: `widgets.css → templates.css → app.css → bootstrap-icons.min.css` (BUILDING-A-FRONTEND.md:160-167).
- وضع RTL افتراضيّاً عبر `<html dir="rtl" lang="ar">`.

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § STYLING-METHODOLOGY.md` — القوانين 1-5.
- `ASHARE-METHODOLOGY-CITATIONS.md § ASHARE-PAGE-MIGRATION.md` — Mobile-first في 390×844.

**معايير القبول**:
- `scripts/verify-widget-contracts.sh` → كلّ widget مُستخدَم يُعلن `CSS_CLASSES` في header.
- `scripts/verify-design-quality.sh` → عدد hex في المشروع ≤60 (DESIGN-CRITERIA.md:13).
- `scripts/verify-runtime.mjs --viewport 390x844` → لا overflow أفقيّ، bottom-nav ثابتة في الأسفل.
- `spatial-contracts.json` يحوي عقد `main-layout.header.height ≤ 64` و`main-layout.bottom-nav.height ≥ 56`.

**Definition of Done**: `/` فارغة تعرض شريطاً علويّاً + تخطيطاً + شريطاً سفليّاً ضمن `acs-page` بدون أيّ مخالفة.

**Commit**: `feat(ashare-v2): widgets-only MainLayout + brand tokens`.

### المرحلة 2 — المصادقة والثنائيّات القانونيّة

**النطاق (7 صفحات من الجرد)**:
| القديم | الجديد | مسار |
|---|---|---|
| `Login.razor` (667) | `Pages/Auth/Login.razor` | `/login` |
| `Auth/Register.razor` (stub) | يُحذَف — لا تسجيل يدويّ | — |
| `Language.razor` (165) | `Pages/Language.razor` | `/language` |
| `LegalPageView.razor` (220) | `Pages/Legal/LegalPage.razor` | `/legal/{Key}` |

**التحويلات**:
- `NafathClient` القديم ينفصل — يُستبدَل بعمليّات:
  - `auth.nafath.initiate` (`Tag("client_dispatch","true")` + `ProfileId` في `From` + `Nafath` في `To`).
  - `auth.nafath.poll` (محلّيّ — `ClientOpEngine` يستدعي `HttpDispatcher` دوريّاً).
  - `auth.nafath.complete` — يُنتج JWT ويُسلّمه لـ `TokenManager`.
- استعادة الـ JWT بعد reload عبر `OnAfterRenderAsync(firstRender)` → `await Auth.EnsureRestoredAsync()` (CLAUDE.md:71-74, BUILDING-A-FRONTEND.md:271-274).
- تبديل اللغة = عمليّة `culture.switch` يردّها `CultureInterceptor` الخلفيّ (ASHARE-V2-METHODOLOGY.md:236-304).
- `LegalPage` يستدعي عمليّة `legal.page.get(key)` → envelope يحمل `LegalPage` (العنوان + Html).

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § BUILDING-A-FRONTEND.md` (271-274) — إعادة تحميل الـ Auth.
- `ASHARE-METHODOLOGY-CITATIONS.md § ASHARE-V2-METHODOLOGY.md` (236-304) — نمط UserCulture الموحَّد.

**معايير القبول**:
- Layer 1-6 verify على `/login`, `/language`, `/legal/terms` — 0 violations.
- Playwright: إدخال رقم هويّة → initiate → polling حتى complete → redirect إلى `/` مع JWT في storage.
- إعادة تحميل `/` بعد تسجيل الدخول → يبقى المستخدم مُسجَّلاً (Layer 5 auth-restore).
- تبديل اللغة من `ar` إلى `en` يغيّر `dir` من `rtl` إلى `ltr` والمحتوى من `L["..."]`.

**Definition of Done**: مستخدم تجريبيّ يسجّل دخوله بنفاذ ويرى الصفحة الرئيسيّة بالعربيّة أو الإنجليزيّة.

**Commit**: `feat(ashare-v2): auth flow via Nafath operations + culture switch + legal pages`.

### المرحلة 3 — الاستكشاف (Home / Search / Explore)

**النطاق (3 صفحات)**:
| القديم | الجديد | مسار |
|---|---|---|
| `Home.razor` (234) | `Pages/Home.razor` | `/` |
| `Search.razor` (349) | `Pages/Search.razor` | `/search` |
| `Explore.razor` (467) | `Pages/Explore.razor` | `/explore` |

**التحويلات**:
- `AshareApiService.GetFeaturedListingsAsync()` → عمليّة `listings.featured` (Tag `client_dispatch=true`).
- `AshareApiService.SearchAsync(filters)` → عمليّة `listings.search` تحمل `SearchFilters` في `Tags`.
- `AshareApiService.GetCategoriesAsync()` → عمليّة `categories.list`.
- Memory cache القديم يُستبدَل بـ interceptor `CacheInterceptor` مطابق لـ `Tag("cacheable","true")` (نمط مشابه لـ LIBRARY-ANATOMY.md:148-155).
- كلّ قائمة مساحات تُقدَّم عبر `<AcListingGrid>` widget جديد (widget موحَّد يُستخدَم في Home/Search/Explore/Favorites/Host).
- شريط البحث `<AcSearchBar>` يوجّه إلى `/search` مع query string.
- الفلاتر (الفئة/المدينة/السعر) في `<AcFilterSheet>` — bottom-sheet متحرّكة.

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § ASHARE-V2-METHODOLOGY.md` (112-129) — HTTP-bound vs local-only.
- `ASHARE-METHODOLOGY-CITATIONS.md § ASHARE-PAGE-MIGRATION.md` (18) — منع `AshareApiService`.
- `ASHARE-METHODOLOGY-CITATIONS.md § DYNAMIC-ATTRIBUTES.md` (13-20) — استهلاك Snapshot السمات للبطاقات.

**معايير القبول**:
- `scripts/verify-page-structure.sh` → صفر.
- Playwright على 390×844: شريط البحث يصل لـ `/search?q=...` خلال ≤ثانيتَين.
- سلّم الخطوط: كلّ عناوين البطاقات `16/18/20 px` فقط (DESIGN-CRITERIA.md:15).
- `spatial-contracts.json` يعرّف `home.featured-grid.card.min-height >= 180`.

**Definition of Done**: زائر يتصفّح الرئيسيّة → Explore → Search مع فلاتر، وكلّ قائمة تُحمَّل من عمليّات OAM خلفيّة.

**Commit**: `feat(ashare-v2): browsing pages on listings operations + unified listing grid widget`.

### المرحلة 4 — تفاصيل المساحة (أكبر صفحة: 1,455 سطراً)

**النطاق**:
| القديم | الجديد | مسار |
|---|---|---|
| `SpaceDetails.razor` (1,455) | `Pages/Space/SpaceDetails.razor` + أجزاء | `/space/{SpaceId:guid}` |

**تقسيم الصفحة إلى ودجات قابلة لإعادة الاستخدام**:
- `<AcSpaceGallery>` — معرض صور + lazy loading + full-screen.
- `<AcSpaceHeader>` — عنوان + موقع + سعر + مشاركة + مفضّلة.
- `<AcSpaceAttributes>` — DynamicAttributes snapshot يُعرَض حسب Template الفئة (DYNAMIC-ATTRIBUTES.md:13-20). السمات غير المعروفة تُعرَض كـ chips (DYNAMIC-ATTRIBUTES.md:47-54).
- `<AcSpaceHostCard>` — بطاقة المُستضيف + زرّ محادثة.
- `<AcSpaceActions>` — أزرار حجز/محادثة (sticky أسفل الشاشة).

**العمليّات**:
- `listings.get(id)` → envelope يحمل `ProductListing` + `Product` + `Category` + سمات ديناميكيّة.
- `favorites.toggle(listingId)` محلّيّ + HTTP (Tag `client_dispatch=true`).
- `chats.start(withUserId, contextListingId)` → envelope يحوي `ChatId` → navigate إلى `/chat/{id}`.
- مشاركة = عمليّة محلّية `space.share` تستدعي Web Share API (ASHARE-V2-METHODOLOGY.md:112-129 — local-only).

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § DYNAMIC-ATTRIBUTES.md` — عرض السمات غير المعروفة.
- `ASHARE-METHODOLOGY-CITATIONS.md § CLAUDE.md` (Law 6) — التكيّف مع بيانات الإنتاج (`features` vs `amenities`).
- `ASHARE-METHODOLOGY-CITATIONS.md § STYLING-METHODOLOGY.md` (82-98) — صفحة widevar: `acs-page-wide`.

**معايير القبول**:
- Layer 1-6 verify → 0 violations.
- Playwright: فتح مساحة → المعرض يتحرّك أفقيّاً، الـ sticky actions ظاهرة، زرّ الحجز يفتح `/book/{id}`.
- `spatial-contracts.json` يُلزم: `space.gallery.aspect-ratio in [1.0, 1.8]`, `space.actions.bottom-offset == 0` عند السكون.
- مطابقة بصريّة مع النسخة القديمة في 390×844 (ASHARE-PAGE-MIGRATION.md:105-118).

**Definition of Done**: صفحة تفاصيل تعرض مساحة من الإنتاج بكلّ سماتها، أزرار المحادثة والحجز تعملان.

**Commit**: `feat(ashare-v2): space details page split into reusable widgets + dynamic attrs`.

### المرحلة 5 — مسار الحجز (Bookings)

**النطاق (3 صفحات)**:
| القديم | الجديد | مسار |
|---|---|---|
| `Bookings.razor` (413) | `Pages/Booking/Bookings.razor` | `/bookings` |
| `BookingCreate.razor` (965) | `Pages/Booking/BookingCreate.razor` | `/book/{SpaceId:guid}` |
| `BookingDetails.razor` (988) | `Pages/Booking/BookingDetails.razor` | `/booking/{BookingId:guid}` |

**العمليّات**:
- `bookings.list.mine(status)` — قائمة حجوزات المستخدم (active/past/cancelled).
- `bookings.quote(spaceId, range)` — تسعير أوّليّ قبل التثبيت.
- `bookings.create` — العمليّة الأساسيّة:
  ```csharp
  Entry.Create("booking.create")
    .From($"User:{customerId}", 1, ("role","customer"))
    .To($"Listing:{listingId}",  1, ("role","target"))
    .Tag("start", start).Tag("end", end).Tag("price", price)
    .Tag("client_dispatch","true")
    .Analyze(new RequiredFieldAnalyzer("start", () => start))
    .Analyze(new RequiredFieldAnalyzer("end",   () => end))
    .Build();
  ```
- `bookings.cancel(id, reason)`, `bookings.confirm(id)` (للمُستضيف), `bookings.reject(id, reason)`.
- `payments.initiate(bookingId)` → envelope يحمل `paymentUrl` (Noon redirect) — يُفتَح في `<PaymentWebView>` على MAUI أو يُوجَّه كـ top-level على Web.

**قيود**:
- استعادة الـ Auth قبل أيّ تحميل (CLAUDE.md:71-74).
- التحويل بين الأرقام العربيّة/اللاتينيّة في التواريخ يعالجه `NumeralToLatinSaveInterceptor` خلفيّاً (CULTURE.md:33-34) — الواجهة تعرض بحسب ثقافة المستخدم.

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § CLAUDE.md` (Law 1) — كلّ طفرة = Entry.
- `ASHARE-METHODOLOGY-CITATIONS.md § MODEL.md` (65-82) — Entry minimum: From + To + Balance.

**معايير القبول**:
- Layer 1-6 verify على كلّ صفحة.
- Playwright: اختيار مدّة → quote يظهر → تأكيد → redirect دفع → callback → `/booking/{id}` يعرض "مدفوع".
- فحص yاعتمادٌ على `scripts/verify-runtime-auth.mjs` للتحقّق من سلوك استعادة الـ Auth.
- `spatial-contracts.json`: `booking-create.actions.sticky == true`, `booking-details.status-badge.top-aligned == true`.

**Definition of Done**: مستخدم يحجز مساحة ثم يرى التفاصيل ويلغي أو يدفع.

**Commit**: `feat(ashare-v2): bookings list + create + details via OAM operations`.

### المرحلة 6 — مسار المُستضيف (Host)

**النطاق (6 صفحات)**:
| القديم | الجديد | مسار |
|---|---|---|
| `CreateListing.razor` (585) | `Pages/Host/CreateListing.razor` | `/create-listing`, `/host/add` |
| `Host/MySpaces.razor` (599) | `Pages/Host/MySpaces.razor` | `/host/spaces` |
| `Host/SubscriptionPlans.razor` (296) | `Pages/Host/SubscriptionPlans.razor` | `/host/plans` |
| `Host/SubscriptionCheckout.razor` (828) | `Pages/Host/SubscriptionCheckout.razor` | `/host/subscribe/{PlanSlug}` |
| `Host/SubscriptionDashboard.razor` (352) | `Pages/Host/SubscriptionDashboard.razor` | `/host/subscription[/dashboard]` |
| `Host/PaymentCallback.razor` (326) | `Pages/Host/PaymentCallback.razor` | `/host/payment/callback` |

**CreateListing — معالج wizard مبنيّ على عمليّات**:
- خطوة 1 (الفئة) → عمليّة محلّية `listing.draft.setCategory` تحفظ Draft في localStorage + تجلب `AttributeTemplate` للفئة.
- خطوة 2 (السمات) → form ديناميكيّ مبنيّ على Template (DYNAMIC-ATTRIBUTES.md:13-20).
- خطوة 3 (الصور) → رفع عبر عمليّة `files.upload` تعيد `FileUrl`.
- خطوة 4 (المراجعة والإرسال) → عمليّة `listing.create`:
  ```csharp
  Entry.Create("listing.create")
    .From($"User:{hostId}", 1, ("role","host"))
    .To($"Listing:{newId}", 1, ("role","created"))
    .Tag("category", categoryId).Tag("attributes-snapshot", snapshotJson)
    .Tag("client_dispatch","true")
    .Analyze(new RequiredFieldAnalyzer("title", () => title))
    .Build();
  ```
- Draft محلّيّ يعيش في `IStorageService` — local-only عمليّة (ASHARE-V2-METHODOLOGY.md:112-129).

**الاشتراكات**:
- `subscriptions.plans.list` — يجلب الخطط.
- `subscriptions.checkout(planSlug)` — ينشئ طلب دفع ويعيد `paymentUrl`.
- `payments.callback.ack(token)` — الخلفيّة تعالج الـ callback ثم تُرسَل إشعار SignalR → الصفحة ترى الحالة الجديدة.
- `subscriptions.current` — حالة الاشتراك.

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § DYNAMIC-ATTRIBUTES.md` — Template-driven forms.
- `ASHARE-METHODOLOGY-CITATIONS.md § CLAUDE.md` (Law 6) — الحفاظ على مفاتيح سمات غير معروفة.

**معايير القبول**:
- Layer 1-6 على كلّ الصفحات الستّ.
- Playwright: مُستضيف ينشر مساحة جديدة → تظهر في `/host/spaces` → يشترك في خطّة → يرى "فعّالة" في Dashboard.
- `spatial-contracts.json`: `host-create.wizard.progress-bar.visible == true`, `host-plans.cards.count in [2,5]`.
- `verify-design-quality.sh`: لا ألوان hex في حقول الإدخال (DESIGN-CRITERIA.md:14).

**Definition of Done**: دورة كاملة من نشر مساحة إلى اشتراك فعّال.

**Commit**: `feat(ashare-v2): host flow — listing creation wizard + subscription checkout`.

### المرحلة 7 — المحادثات والإشعارات الفوريّة (Realtime)

**النطاق (3 صفحات + تكامل Realtime)**:
| القديم | الجديد | مسار |
|---|---|---|
| `Chat/Chats.razor` (227) | `Pages/Chat/Chats.razor` | `/chats` |
| `Chat/ChatRoom.razor` (455) | `Pages/Chat/ChatRoom.razor` | `/chat/{ChatId:guid}` |
| `Notifications.razor` (170) | `Pages/Notifications.razor` | `/notifications` |

**Realtime**:
- القديم: `RealtimeClient` مع SignalR hubs `/hubs/chat` و`/hubs/notifications`.
- الجديد: Provider contract `IRealtimeClient` (LIBRARY-ANATOMY.md:69-127) يُحقن من shell (Web يستخدم SignalR، MAUI لاحقاً يستخدم نفس الـ contract).
- الصفحة تستقبل الحدث → تُصدر عمليّة محلّية `chat.message.received` أو `notifications.received` → `OperationInterpreterRegistry` يحدّث الـ UI.
- لا اتّصال مباشر بين الصفحة والـ hub — فقط عبر الـ provider وعمليّات OAM.

**العمليّات**:
- `chats.list.mine` — قائمة المحادثات.
- `chats.messages.list(chatId, before?, take)` — تصفّح الرسائل.
- `chats.message.send(chatId, content)` — إرسال رسالة (Entry بين Sender و Chat).
- `notifications.list.mine(unreadOnly?)` — قائمة الإشعارات.
- `notifications.mark.read(id)` — تحديث الحالة.

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § LIBRARY-ANATOMY.md` (69-127) — ProviderContract.
- `ASHARE-METHODOLOGY-CITATIONS.md § ASHARE-V2-METHODOLOGY.md` (15-24) — دورة client/server opertation.

**معايير القبول**:
- Layer 1-6 verify → 0 violations.
- Playwright مزدوج (جلستان): A يرسل رسالة لـ B → B يراها خلال ≤2 ثانية.
- إعادة الاتّصال بعد انقطاع Realtime تُستأنَف الرسائل المفقودة.
- `spatial-contracts.json`: `chat-room.input.sticky-bottom == true`, `notifications.item.height >= 72`.

**Definition of Done**: محادثتان متزامنتان تتبادلان رسائل مع إشعارات push داخل التطبيق.

**Commit**: `feat(ashare-v2): chats + notifications via IRealtimeClient provider`.

### المرحلة 8 — البروفايل والمفضّلات

**النطاق (3 صفحات)**:
| القديم | الجديد | مسار |
|---|---|---|
| `Profile.razor` (483) | `Pages/Profile/Profile.razor` | `/profile` |
| `Auth/ProfileEdit.razor` (382) | `Pages/Profile/ProfileEdit.razor` | `/profile/edit` |
| `Favorites.razor` (122) | `Pages/Favorites.razor` | `/favorites` |

**العمليّات**:
- `profiles.me` — بيانات المستخدم.
- `profiles.update` — تحديث الاسم/الوصف/التفضيلات.
- `profiles.avatar.update(fileUrl)` — بعد `files.upload` يأتي ربط بالـ Profile.
- `favorites.list.mine` + `favorites.toggle(listingId)` (المرحلة 4 شاركت الـ toggle).
- `auth.logout` — محلّيّ + HTTP → يمسح `TokenManager`.

**تكييف مع بيانات الإنتاج (CLAUDE.md:76-85)**:
- إن كان Profile الإنتاج يحوي حقل `nickname` بدل `displayName`، يُحفَظ كما هو ويُعرَض.
- أيّ مفتاح غير معروف في response يُحفَظ في `DynamicAttribute` للعرض التالي.

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § BUILDING-A-FRONTEND.md` (317-374) — الصفحات المعتمدة على Auth.
- `ASHARE-METHODOLOGY-CITATIONS.md § CLAUDE.md` (Law 5, Law 6).

**معايير القبول**:
- Layer 1-6 → 0 violations.
- Playwright: تحرير الاسم → حفظ → ظهور الاسم الجديد في `MainLayout`.
- رفع صورة → ضغط → تحديث الـ avatar.
- قائمة المفضّلة تُحمَّل حتى بعد إعادة تحميل الصفحة (Law 5).

**Definition of Done**: مستخدم يدير بروفايله ومفضّلاته بالكامل.

**Commit**: `feat(ashare-v2): profile + favorites pages on OAM operations`.

### المرحلة 9 — الشكاوى (Complaints)

**النطاق (صفحتان)**:
| القديم | الجديد | مسار |
|---|---|---|
| `Complaints.razor` (759) | `Pages/Complaints/Complaints.razor` | `/complaints` |
| `ComplaintDetails.razor` (726) | `Pages/Complaints/ComplaintDetails.razor` | `/complaints/{Id:guid}` |

**العمليّات**:
- `complaints.list.mine` — قائمة شكاوى المستخدم مع الحالة.
- `complaints.create` — إنشاء شكوى:
  ```csharp
  Entry.Create("complaint.create")
    .From($"User:{reporterId}", 1, ("role","reporter"))
    .To($"Complaint:{newId}",   1, ("role","created"))
    .Tag("target", targetRef).Tag("category", category)
    .Analyze(new RequiredFieldAnalyzer("subject", () => subject))
    .Build();
  ```
- `complaints.reply(id, content, attachments[])` — ردّ تبادليّ.
- `complaints.close(id, reason)` — إقفال (للإدارة أو المُبلِّغ).
- رفع المرفقات = `files.upload` ثم ربط بالردّ.

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § MODEL.md` (65-82) — شكل الـ Entry الإلزاميّ.
- `ASHARE-METHODOLOGY-CITATIONS.md § LIBRARY-ANATOMY.md` (148-155) — Audit interceptor للشكاوى تلقائيّاً.

**معايير القبول**:
- Layer 1-6 → 0 violations.
- Playwright: إنشاء شكوى → ظهور في القائمة → فتح التفاصيل → إضافة ردّ → الردّ يظهر للطرف الآخر.
- `spatial-contracts.json`: `complaint-details.thread.gap == 8`, `complaint-details.attachment.chip.max-width == 280`.

**Definition of Done**: دورة شكوى من الإنشاء إلى الإقفال.

**Commit**: `feat(ashare-v2): complaints list + thread pages as OAM operations`.

### المرحلة 10 — لوحة الإدارة (Admin) — استبدال Syncfusion

**المشكلة الحاكمة**: `Ashare.Admin` القديم يعتمد على Syncfusion Blazor، وهذا يخالف القانون 4 (CLAUDE.md:68-69) والقانون 1 في STYLING-METHODOLOGY.md:28-44 الذي يقصر الواجهة على templates + widgets.

**القرار**: إعادة كتابة لوحة الإدارة على `Apps/Ashare.V2.Admin` ببنية widgets-only + قوالب إدارة جديدة `acx-admin-*`.

**النطاق (13 صفحة)**:
| القديم | الجديد | مسار |
|---|---|---|
| `Dashboard` | `Pages/Admin/Dashboard.razor` | `/admin` |
| `Login` | `Pages/Admin/Login.razor` | `/admin/login` |
| `AdminUsers`, `Users`, `UserDetails`, `Roles` | `Pages/Admin/Users/*.razor` | `/admin/users[/{id}]`, `/admin/roles` |
| `Listings` | `Pages/Admin/Listings.razor` | `/admin/listings` |
| `Orders` | `Pages/Admin/Orders.razor` | `/admin/orders` |
| `Subscriptions` | `Pages/Admin/Subscriptions.razor` | `/admin/subscriptions` |
| `Marketing` | `Pages/Admin/Marketing.razor` | `/admin/marketing` |
| `Notifications` | `Pages/Admin/Notifications.razor` | `/admin/notifications` |
| `Reports` | `Pages/Admin/Reports.razor` | `/admin/reports` |
| `Settings` | `Pages/Admin/Settings.razor` | `/admin/settings` |
| `Versions` | `Pages/Admin/Versions.razor` | `/admin/versions` |

**ودجات إداريّة جديدة** (تضاف إلى مكتبة widgets إن لزم):
- `<AcDataTable>` — جدول قابل للفلترة + pagination من Provider contract.
- `<AcStatsCard>` — بطاقة KPI.
- `<AcTimeSeriesChart>` — رسم بيانيّ بسيط (SVG من سلسلة بيانات) — لا مكتبة خارجيّة.
- `<AcBulkActionBar>` — شريط عمليّات جماعيّ.

**المصادقة**:
- `AdminAuthStateProvider` + `AdminTokenProvider` يُستبدَلان بـ `ScopedTokenProvider` موحَّد مع adaptation لصلاحيّات Admin claims.

**DataProtection**:
- نفس الإعداد القديم `PersistKeysToFileSystem` (لا مشكلة منهجيّة هنا).

**العمليّات**:
- `admin.dashboard.stats` — KPIs.
- `admin.users.list(filter)` / `admin.users.get(id)` / `admin.users.updateRole(id, role)`.
- `admin.listings.moderate(id, decision)`.
- `admin.notifications.broadcast(audience, payload)`.
- `admin.reports.{name}` — تقارير مختلفة.
- كلّ طفرة تحمل `Tag("audit","true")` لتفعيل AuditInterceptor (LIBRARY-ANATOMY.md:148-155).

**الاستشهادات**:
- `ASHARE-METHODOLOGY-CITATIONS.md § STYLING-METHODOLOGY.md` — قانون 1 يمنع Syncfusion.
- `ASHARE-METHODOLOGY-CITATIONS.md § LIBRARY-ANATOMY.md` (148-155) — Audit interceptor.

**معايير القبول**:
- Layer 1-6 على كلّ صفحة إداريّة.
- Grep على مصدر `Apps/Ashare.V2.Admin` → صفر نتائج لـ `Syncfusion` و`SfGrid`.
- `scripts/verify-widget-contracts.sh Apps/Ashare.V2.Admin` → 0 violations.
- Playwright: مشرف يُبثّ إشعاراً → يُستلَم على جلسة مستخدم في محاكاة.

**Definition of Done**: Admin V2 يحلّ محلّ القديم وظيفيّاً (13 صفحة) بدون Syncfusion.

**Commit**: `feat(ashare-v2): admin panel rewritten on widgets — no Syncfusion`.

### المرحلة 11 — Shell الـ MAUI (مُؤجَّلة بعد استقرار Web)

**المبرّر**: V2 الحاليّة تستهدف Blazor Server أوّلاً. نقل MAUI يحتاج:
- `MauiStorageService` (محلّيّ، يشابه `BrowserStorageService`).
- Firebase init في `AppDelegate.cs` iOS + `CrossFirebase.Initialize` على Android (مستنسَخ من `MauiProgram.cs:44-52`).
- `PaymentWebViewHandler` لـ 3DS/OTP — نفس الـ handler القديم (Android WebView).
- `PushNotificationService` → Provider contract `IPushNotificationService` (LIBRARY-ANATOMY.md:69-127) يُنفَّذ محلّيّاً على Android/iOS/Windows.
- `SkiaImageCompressionService` + `MauiMediaPickerService` + `DeviceTimezoneService` + `MauiDeviceInfoProvider` + `AttributionCaptureService` — كلّها تنتقل كما هي إلى provider contracts.
- إعادة استخدام كلّ صفحات `Apps/Ashare.V2.Web` عبر `AddAdditionalAssemblies`.

**معايير القبول**:
- التطبيق يُقلع على Android/iOS ويعرض الرئيسيّة.
- FCM token يُسجَّل عبر عمليّة `notifications.device.register`.
- الدفع يفتح WebView → يعود → الحجز يعكس النتيجة.
- spatial contracts تمرّ على 390×844 (ASHARE-PAGE-MIGRATION.md:105-118).

**Definition of Done**: بناءان حزم Release لـ Android و iOS قابلان للإطلاق على المتاجر.

**Commit**: `feat(ashare-v2): MAUI shell for iOS + Android with platform providers`.

---

## 2. مصفوفة معايير القبول (Acceptance Matrix)

| المعيار | السكربت | العتبة | المراجع |
|---|---|---|---|
| Code Hygiene | `scripts/verify-page-structure.sh` | 0 violations | VERIFICATION-LAYERS.md:86 |
| Class Existence | `scripts/verify-css.sh` | 0 undefined classes | VERIFICATION-LAYERS.md:91 |
| Per-Value Scale | `scripts/verify-design-tokens.sh` | فونت على `10..48` فقط، تباعد مضاعفات 4 | VERIFICATION-LAYERS.md:95-98, DESIGN-CRITERIA.md:15-16 |
| Widget Contracts | `scripts/verify-widget-contracts.sh` | كلّ class مُستخدَمة مُعلَنة في header widget | STYLING-METHODOLOGY.md:50-56 |
| Design Quality | `scripts/verify-design-quality.sh` | hex ≤60، لا ألوان inline | DESIGN-CRITERIA.md:13-14 |
| Runtime Spatial | `scripts/verify-runtime.sh` + `spatial-contracts.json` | كلّ العقد تمرّ على 390×844 | VERIFICATION-LAYERS.md:119, ASHARE-PAGE-MIGRATION.md:105-118 |
| Auth Restore | `scripts/verify-runtime-auth.mjs` | الجلسة تنجو بعد reload | CLAUDE.md:71-74, BUILDING-A-FRONTEND.md:271-274 |
| No legacy client | grep: `AshareApiService\|bi bi-\|<button class="btn` | 0 في `Apps/Ashare.V2.*` | ASHARE-PAGE-MIGRATION.md:11-19 |

**لكلّ مرحلة قبل الدمج**: تُشغَّل كلّ السكربتات أعلاه على مجلّد التطبيق (وليس على الصفحة فقط) — النتيجة `0 violations` على كلّ عمود، وإلّا تُحلّ الأسباب الجذريّة ولا يتقدّم العمل.

---

## 3. فهرس الاستشهادات المنهجيّة

كلّ مرجع أدناه له نصّ بالحرف في `docs/ASHARE-METHODOLOGY-CITATIONS.md` مع `file:line`:

- **CLAUDE.md** — القوانين الستّة (41-85).
- **MODEL.md** — بنية الـ Entry (65-82)، Analyzer vs Interceptor (83-123)، Sealed/Exclude (122-123)، ProviderContract (125-142).
- **LIBRARY-ANATOMY.md** — الطبقات الثلاث (33-44, 69-127, 128-155).
- **BUILDING-A-FRONTEND.md** — الـ cascade (160-167)، ملفّ العلامة (176-208)، استعادة Auth (271-274)، auth-dependent pages (317-374).
- **STYLING-METHODOLOGY.md** — القوانين الخمسة (28-98).
- **DESIGN-CRITERIA.md** — سقف اللوحة (13)، منع ألوان inline (14)، سلّم الخطوط (15)، سلّم التباعد (16).
- **VERIFICATION-LAYERS.md** — الطبقات الستّ (86-119).
- **CULTURE.md** — تركيب الواجهة (54-62)، `NumeralToLatinSaveInterceptor` (33-34).
- **DYNAMIC-ATTRIBUTES.md** — Template + Snapshot (13-20)، مفاتيح غير معروفة (47-54).
- **ASHARE-V2-METHODOLOGY.md** — HTTP vs local (112-129)، UserCulture الموحَّد (236-304)، دورة Operation (15-24).
- **ASHARE-PAGE-MIGRATION.md** — القواعد الخمس غير القابلة للتفاوض (11-19)، mobile-first (105-118)، خطّ التحقّق السّداسيّ (121-145).

---

## 4. سجلّ التغييرات

- **2026-04-18** — النسخة الأولى من الخطّة: 12 مرحلة + مصفوفة قبول + فهرس استشهادات.
