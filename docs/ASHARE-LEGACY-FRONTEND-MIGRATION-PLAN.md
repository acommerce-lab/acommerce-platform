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
- **2026-04-18 (تعديل)** — إضافة 5 ملاحق تفصيليّة (أ–هـ) لاستعادة التفاصيل المُقتضبة في النسخة الأولى.

---

## ملحق أ — جدول تفصيليّ للصفحات الـ25

مستخلَص من `docs/ASHARE-LEGACY-FRONTEND-INVENTORY.md § 2.1`. العمود "Injected" يسرد الخدمات/العملاء المحقونين في `@inject` بالصفحة القديمة — كلّ هؤلاء يُعاد تفسيرهم كعمليّات OAM (ما لم يُذكر خلاف ذلك).

| # | الصفحة | المسار | Lines | Injected في القديم | المرحلة |
|---:|---|---|---:|---|---:|
| 1 | `Home.razor` | `/` | 234 | `AshareApiService`, `IAppNavigationService` | 3 |
| 2 | `Search.razor` | `/search` | 349 | `AshareApiService` | 3 |
| 3 | `Explore.razor` | `/explore` | 467 | `AshareApiService`, `CategoriesClient` | 3 |
| 4 | `SpaceDetails.razor` | `/space/{SpaceId:guid}` | 1,455 | `AshareApiService`, `ChatsClient`, `ProfilesClient`, `TokenManager`, `IJSRuntime` | 4 |
| 5 | `Login.razor` | `/login` | 667 | `NafathClient`, `AuthClient`, `RealtimeClient`, `TokenManager`, `GuestModeService`, `AppLifecycleService`, `ThemeService`, `ILocalizationService L` | 2 |
| 6 | `Auth/Register.razor` | `/register` | 16 | — (stub؛ يُحذَف) | 2 |
| 7 | `Language.razor` | `/language` | 165 | `LocalizationService` | 2 |
| 8 | `LegalPageView.razor` | `/legal/{Key}` | 220 | `LegalPagesClient` | 2 |
| 9 | `Favorites.razor` | `/favorites` | 122 | `AshareApiService` | 8 |
| 10 | `Profile.razor` | `/profile` | 483 | `ProfilesClient`, `TokenManager`, `AuthClient` | 8 |
| 11 | `Auth/ProfileEdit.razor` | `/profile/edit` | 382 | `ProfilesClient`, `FilesClient` | 8 |
| 12 | `Notifications.razor` | `/notifications` | 170 | `NotificationsClient` | 7 |
| 13 | `Chat/Chats.razor` | `/chats` | 227 | `ChatsClient` | 7 |
| 14 | `Chat/ChatRoom.razor` | `/chat/{ChatId:guid}` | 455 | `ChatsClient`, `RealtimeClient` | 7 |
| 15 | `Bookings.razor` | `/bookings` | 413 | `BookingsClient`, `AshareApiService` | 5 |
| 16 | `BookingCreate.razor` | `/book/{SpaceId:guid}` | 965 | `BookingsClient`, `PaymentsClient`, `ProductListingsClient`, `ProfilesClient`, `ITimezoneService` | 5 |
| 17 | `BookingDetails.razor` | `/booking/{BookingId:guid}` | 988 | `BookingsClient`, `PaymentsClient`, `ChatsClient`, `IPaymentService` | 5 |
| 18 | `CreateListing.razor` | `/create-listing`, `/host/add` | 585 | `ProductListingsClient`, `FilesClient`, `CategoryAttributesClient`, `PendingListingService` | 6 |
| 19 | `Host/MySpaces.razor` | `/host/spaces` | 599 | `ProductListingsClient`, `ProfilesClient` | 6 |
| 20 | `Host/SubscriptionPlans.razor` | `/host/plans` | 296 | `SubscriptionClient`, `AshareSubscriptionPlans` | 6 |
| 21 | `Host/SubscriptionCheckout.razor` | `/host/subscribe/{PlanSlug}` | 828 | `SubscriptionClient`, `PaymentsClient`, `IPaymentService`, `RealtimeClient` | 6 |
| 22 | `Host/SubscriptionDashboard.razor` | `/host/subscription[/dashboard]` | 352 | `SubscriptionClient` | 6 |
| 23 | `Host/PaymentCallback.razor` | `/host/payment/callback` | 326 | `RealtimeClient`, `IAppNavigationService` | 6 |
| 24 | `Complaints.razor` | `/complaints` | 759 | `ComplaintsClient` | 9 |
| 25 | `ComplaintDetails.razor` | `/complaints/{Id:guid}` | 726 | `ComplaintsClient`, `FilesClient` | 9 |
| | **الإجماليّ** | | **~12,249** | | |

**ملاحظة تحويليّة**: كلّ استخدام لـ `AshareApiService` يُستبدَل بعمليّة `Entry.Create(...)` محدّدة (القانون 4 في ASHARE-PAGE-MIGRATION.md:18). لم يَعُد هناك خدمة غلاف واحدة.

---

## ملحق ب — مطابقة فئات وأيقونات قديمة إلى widgets

القاعدة (ASHARE-PAGE-MIGRATION.md:11-14): لا `<button>/<input>/.btn/.card` خام ولا `<i class="bi bi-*">` — كلّ مرئيّ عبر `Ac*`.

### ب-1. ودجات الإدخال والأفعال

| القديم | الجديد |
|---|---|
| `<button class="btn btn-primary">` | `<AcButton Variant="primary">` |
| `<button class="btn btn-outline-primary">` | `<AcButton Variant="outline">` |
| `<button class="btn btn-danger">` | `<AcButton Variant="danger">` |
| `<button class="btn btn-link">` | `<AcButton Variant="text">` |
| `<input class="form-control">` | `<AcTextField>` |
| `<textarea class="form-control">` | `<AcTextArea>` |
| `<select class="form-select">` | `<AcSelect>` |
| `<input type="checkbox">` | `<AcCheckbox>` |
| `<input type="radio">` | `<AcRadio>` |
| `<input type="file">` | `<AcFilePicker>` + عمليّة `files.upload` |

### ب-2. ودجات الحاوية والعرض

| القديم | الجديد |
|---|---|
| `<div class="card">` / `ashare-card` | `<AcCard>` |
| `<div class="card-body">` | `<AcCardBody>` |
| `<div class="modal">` | `<AcDialog>` |
| `<nav class="navbar">` (علويّ) | `<AcAppBar>` |
| `<nav class="bottom-nav">` | `<AcBottomNav>` + `<AcBottomNavItem>` |
| `<div class="alert alert-*">` | `<AcAlert Severity="...">` |
| `<span class="badge">` / `ashare-badge` | `<AcBadge>` |
| `<ul class="list-group">` | `<AcList>` + `<AcListItem>` |
| `<img class="avatar">` / `ashare-avatar` | `<AcAvatar>` |
| `<div class="spinner-border">` | `<AcSpinner>` |
| `<div class="skeleton">` | `<AcSkeleton>` |
| `<div class="toast">` | `<AcToast>` (عبر `IToastService`) |
| `ashare-chip` | `<AcChip>` |
| `ashare-tabs` / `nav nav-tabs` | `<AcTabs>` + `<AcTab>` |

### ب-3. الأيقونات (Bootstrap Icons → AcIcon)

| القديم | الجديد |
|---|---|
| `<i class="bi bi-heart">` / `bi-heart-fill` | `<AcIcon Name="heart" Filled="true/false">` |
| `<i class="bi bi-chat">` | `<AcIcon Name="chat">` |
| `<i class="bi bi-bell">` | `<AcIcon Name="bell">` |
| `<i class="bi bi-geo-alt">` | `<AcIcon Name="map-pin">` |
| `<i class="bi bi-calendar">` | `<AcIcon Name="calendar">` |
| `<i class="bi bi-star-fill">` | `<AcIcon Name="star" Filled="true">` |
| `<i class="bi bi-person-circle">` | `<AcIcon Name="user-circle">` |
| `<i class="bi bi-three-dots">` | `<AcIcon Name="more-horizontal">` |
| `<i class="bi bi-arrow-left">` | `<AcIcon Name="arrow-left">` (يُعكَس تلقائيّاً في RTL) |
| `<i class="bi bi-filter">` | `<AcIcon Name="filter">` |
| `<i class="bi bi-search">` | `<AcIcon Name="search">` |
| `<i class="bi bi-upload">` | `<AcIcon Name="upload">` |
| `<i class="bi bi-trash">` | `<AcIcon Name="trash">` |
| `<i class="bi bi-check-circle">` | `<AcIcon Name="check-circle">` |
| `<i class="bi bi-x-circle">` | `<AcIcon Name="x-circle">` |
| `<i class="bi bi-exclamation-triangle">` | `<AcIcon Name="alert-triangle">` |

### ب-4. ألوان الصفحة (hex خام → tokens)

| القديم (في `.razor`) | الجديد |
|---|---|
| `style="color:#345454"` | `style="color:var(--ac-primary)"` أو `class="text-primary"` |
| `style="background:#FEE8D6"` | `var(--ac-bg)` |
| `style="color:#F4844C"` | `var(--ac-secondary)` |
| `style="color:#10B981"` | `var(--ac-success)` |
| `style="color:#EF4444"` | `var(--ac-error)` |
| `style="color:#F59E0B"` | `var(--ac-warning)` |
| `style="color:#FFFFFF"` | `var(--ac-surface)` |

**القاعدة**: كلّ قيمة hex خام في `.razor` = violation في `verify-design-quality.sh` (DESIGN-CRITERIA.md:14).

---

## ملحق ج — خدمات Shell القديمة وما يحلّ محلّها

### ج-1. خدمات Ashare.Web (5 خدمات)

من `/tmp/ACommerce.Libraries/Apps/Ashare.Web/Services/`:

| القديمة | الجديد في V2 | ملاحظة |
|---|---|---|
| `BrowserStorageService : IStorageService` | **تُبقى كما هي** | localStorage عبر JS — لا تغيير منهجيّ |
| `ScopedTokenProvider : ITokenProvider` | **تُبقى كما هي** + يربطها `ClientOpEngine` | Scoped لأنّ Blazor Server scoped per-circuit |
| `WebNavigationService : IAppNavigationService` | **تُبقى كما هي** | يعتمد `NavigationManager` |
| `WebPaymentService : IPaymentService` | عمليّة `payments.redirect(url)` — Provider يحلّ الـ URL في `top-level` | `IPaymentService` يصبح Provider contract |
| `WebAppVersionService : IAppVersionService` | عمليّة `app.version.check` تردّ envelope من `VersionsClient` الخلفيّ | Provider contract |

### ج-2. خدمات Ashare.App (MAUI — تُؤجَّل للمرحلة 11)

من `/tmp/ACommerce.Libraries/Apps/Ashare.App/` (`MauiProgram.cs:81-140`):

| القديمة | Provider contract مقابل | ملاحظة |
|---|---|---|
| `MauiStorageService` | `IStorageService` | حفظ في `Preferences`/`SecureStorage` |
| `StorageBackedTokenStorage` | `ITokenStorage` | يُبقى كما هو |
| `TokenManager` (Singleton) | `ITokenProvider` | Singleton على MAUI، Scoped على Web |
| `TrackingConsentService` + `TrackingConsentInterceptor` | `ITrackingConsentService` + HTTP interceptor | يُرسل هيدر موافقة التتبّع |
| `MauiDeviceInfoProvider` | `IDeviceInfoProvider` | لتقارير الأخطاء |
| `MauiMediaPickerService` | `IMediaPickerService` | كاميرا + معرض |
| `SkiaImageCompressionService` | `IImageCompressionService` | ضغط الصور قبل الرفع |
| `PushNotificationService` (FCM) | `IPushNotificationService` | Firebase Cloud Messaging |
| `AttributionCaptureService` | `IAttributionCaptureService` | deep-link tracking |
| `DeviceTimezoneService` | `ITimezoneService` | ثقافة البروفايل — CULTURE.md:42-62 |
| `MauiPaymentService` | `IPaymentService` | WebView داخل التطبيق |
| `AppVersionService` | `IAppVersionService` | مقارنة النسخة |
| `AppLifecycleService` | `IAppLifecycleService` | foreground/background |
| `AppNavigationService` | `IAppNavigationService` | shell Navigation |

كلّ ما في هذا الجدول = Provider contracts (LIBRARY-ANATOMY.md:69-127): المكتبة الرئيسيّة تُعرّف الواجهات، والـ shell (MAUI/Web) يحقن التنفيذ.

### ج-3. لوحة الإدارة — 13 صفحة

من `/tmp/ACommerce.Libraries/Apps/Ashare.Admin/Pages/`:

| الصفحة | المسار المقترَح | العمليّات الأساسيّة |
|---|---|---|
| `Dashboard` | `/admin` | `admin.dashboard.stats` |
| `Login` | `/admin/login` | `auth.admin.login` (Entry خاصّة بكلمة السرّ) |
| `AdminUsers` / `Users` (مُوحَّدان) | `/admin/users` | `admin.users.list`, `admin.users.search` |
| `UserDetails` | `/admin/users/{id}` | `admin.users.get`, `admin.users.update` |
| `Roles` | `/admin/roles` | `admin.roles.list`, `admin.roles.assign` |
| `Listings` | `/admin/listings` | `admin.listings.list`, `admin.listings.moderate` |
| `Orders` | `/admin/orders` | `admin.orders.list`, `admin.orders.update.status` |
| `Subscriptions` | `/admin/subscriptions` | `admin.subscriptions.list`, `admin.subscriptions.cancel` |
| `Marketing` | `/admin/marketing` | `admin.marketing.stats`, `admin.marketing.attribution.list` |
| `Notifications` | `/admin/notifications` | `admin.notifications.broadcast`, `admin.notifications.targeted` |
| `Reports` | `/admin/reports` | `admin.reports.{name}` (حسب التقرير) |
| `Settings` | `/admin/settings` | `admin.settings.get`, `admin.settings.update` |
| `Versions` | `/admin/versions` | `admin.versions.list`, `admin.versions.publish` |

**الخدمات الخمس المرافقة للـ Admin القديم** (`AdminApiService`, `AdminAuthStateProvider`, `AdminTokenProvider`, `AuthService`, `MarketingAnalyticsService`) تُحَلّ إلى:
- `AdminAuthStateProvider` → `CustomAuthenticationStateProvider` موحَّد مع claims.
- `AdminApiService` → يختفي (كلّ استدعاء = عمليّة).
- `MarketingAnalyticsService` → Provider contract `IMarketingAnalyticsProvider` يُستدعى من interceptor `MarketingEventInterceptor` على Tags `marketing.*`.

---

## ملحق د — wizard `CreateListing` (تفصيل الخطوات الخمس)

الصفحة الأصل `CreateListing.razor` (585 سطراً) تستوعب معالِج إعلان متعدّد الخطوات. في V2 تُنقَل إلى `Pages/Host/CreateListing.razor` مع Draft محلّيّ في `IStorageService` (ASHARE-V2-METHODOLOGY.md:112-129 — local-only).

| خطوة | المدخل | العمليّة | ناتج الحفظ |
|---:|---|---|---|
| 1 | **اختيار الفئة** (سكني / طلب سكن / طلب شريك / إداريّ / تجاريّ) — من جرد `AshareSeedDataService` | `listing.draft.setCategory` (محلّيّة) | `Draft.CategoryId` محفوظ |
| 2 | **الموقع الجغرافيّ** — اختيار المدينة والحيّ + إحداثيّات | `listing.draft.setLocation` (محلّيّة) + استدعاء `locations.search` (HTTP) لاقتراحات | `Draft.Location { City, District, Lat, Lng }` |
| 3 | **السمات الديناميكيّة** — form يُبنى من `AttributeTemplate` الخاصّ بالفئة (DYNAMIC-ATTRIBUTES.md:13-20) | `listing.draft.setAttributes` (محلّيّة) | `Draft.DynamicAttributesJson` (snapshot) |
| 4 | **الصور والوصف** — رفع صور متعدّدة + عنوان + وصف | `files.upload` لكلّ صورة → `listing.draft.setMedia` | `Draft.ImageUrls[]` + `Draft.Title` + `Draft.Description` |
| 5 | **المراجعة والنشر** — عرض كلّ ما سبق + زرّ نشر | `listing.create` (HTTP — Tag `client_dispatch=true`) — Entry من `Host` إلى `Listing` مع كلّ الحقول | يُنشَأ `Listing` على الخادم + يُمسَح الـ Draft المحلّيّ |

**سلوك التعافي**: إن أُغلق التطبيق بين الخطوات، الـ Draft المحلّيّ يُستعاد عند فتح `/create-listing` مجدّداً (يُعرَض للمستخدم خيار "استئناف" أو "بدء جديد").

**سمات غير معروفة**: إن أضاف المستخدم حقلاً حرّاً أو قدم الـ API لاحقاً حقولاً جديدة، تُحفَظ كـ `DynamicAttribute` خام بنوع مستنتَج (DYNAMIC-ATTRIBUTES.md:47-54) — لا تُحذف (القانون 6 في CLAUDE.md:76-85).

**معالجات محلّية (Analyzers) على `listing.create`**:
- `RequiredFieldAnalyzer("title", () => draft.Title)`.
- `RequiredFieldAnalyzer("price", () => draft.Price)`.
- `RequiredFieldAnalyzer("categoryId", () => draft.CategoryId)`.
- `PredicateAnalyzer("images_min_1", ctx => draft.ImageUrls.Count >= 1)`.
- `PredicateAnalyzer("location_set", ctx => draft.Location is not null)`.

---

## ملحق هـ — مراجع متقاطعة مع مستندات المستودع

**مستندات الواجهة في `/home/user/acommerce-platform/docs/` ذات الصلة**:

| المستند | ما يستخدَم منه في هذه الخطّة |
|---|---|
| `ASHARE-V2-METHODOLOGY.md` | نمط `ClientOpEngine` + `HttpDispatcher` + `OperationInterpreterRegistry` (المرحلة 0) + UserCulture الموحَّد (المرحلة 2) + HTTP vs local (المراحل 3, 4, 6) |
| `ASHARE-PAGE-MIGRATION.md` | القواعد الخمس غير القابلة للتفاوض (كلّ المراحل) + خطّ التحقّق السّداسيّ (مصفوفة القبول) + mobile-first 390×844 (spatial-contracts) |
| `ASHARE-PAGES-INVENTORY.md` | مرجع مقارن لحالة صفحات V1 داخل المستودع الحاليّ — يُستخدَم لتحديد نمط تطبيقيّ لا كمصدر |
| `ASHARE-V2-GAP-REPORT.md` | يُشير إلى الفجوات بين V1 الحاليّة وV2 — يُقرأ قبل بدء أيّ مرحلة لتجنّب تكرار مشاكل سابقة |
| `BUILDING-A-FRONTEND.md` | الوصفة القانونيّة لتسجيل الخدمات + cascade + brand overrides + auth-dependent pages |
| `STYLING-METHODOLOGY.md` | القوانين الخمسة — مرجع مرحلة 1 ومرحلة 10 (Admin) |
| `DESIGN-CRITERIA.md` | سلالم الخط والتباعد + سقف اللوحة اللونيّة |
| `VERIFICATION-LAYERS.md` | الـ 6 طبقات وربطها بالـ verify-scripts |
| `CULTURE.md` | AddBlazorCultureStack + BrowserCultureProbe + NumeralToLatinSaveInterceptor (المرحلة 0, 2, 5) |
| `DYNAMIC-ATTRIBUTES.md` | Template + Snapshot (المراحل 4 و6) + سمات غير معروفة |
| `ROADMAP.md` | يُراجَع بعد كلّ مرحلة لوسم التقدّم |
| `LIBRARY-ANATOMY.md` | Provider contracts للـ shells (ملحق ج-2) + Interceptors (Quota/Audit/Marketing) |

**مرجع تطبيقيّ (لا مصدر)**: `Apps/Ashare.V2/*` في المستودع الحاليّ — يُقرأ عند الحاجة لمعرفة «كيف طُبّقت المنهجيّة سابقاً» في صفحة مشابهة، لكنّ الكود يُكتَب من جديد من الجرد القديم + المنهجيّة، لا نقلاً.

**مُخرجات الخطّة المتوقّعة لكلّ مرحلة** (Definition of Done موحَّد):
1. Merge request يضمّ: الكود + تحديث `ROADMAP.md` + نتائج السكربتات الستّة ملتصقة بوصف الـ PR.
2. لقطتا شاشة (Desktop + Mobile 390×844) لكلّ صفحة تضاف، لا لقطات لصفحات لم تتغيّر.
3. قسم في وصف الـ PR بعنوان «Methodology Compliance» يربط التغيير بالقانون/القاعدة/المستند.
