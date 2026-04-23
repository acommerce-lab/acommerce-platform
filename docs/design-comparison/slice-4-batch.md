# Design Comparison — Slice 4 Batch (14 pages + version gate + i18n + dark mode)

هذه الدفعة ترحّل كل الصفحات المتبقّية من عشير القديم إضافةً إلى البنية
التحتيّة التي كانت مؤجَّلة (i18n، الوضع الداكن، حارس الإصدار، مُنتقي
المدينة، محاكاة الدفع، إصلاح توجيه الأدوات).

## الصفحات المُرحَّلة (كلّها ✅ على Layer 6)

| V1 | V2 | Template المُستخدَم |
|---|---|---|
| `Auth/Login.razor` (Nafath) | `/login` | صفحة مخصّصة (choose→nafath/sms→verify) |
| `Profile.razor` | `/me` | صفحة + `AcActionCard` grid |
| `Settings.razor` | `/settings` | صفحة + `AcFilterChip` toggles |
| `Language.razor` | (ضمن `/settings`) | مُدمَج كـ chip-pair |
| `LegalPageView.razor` | `/legal/{key}` | `AcCard` + `<pre>` legal body |
| `Favorites.razor` | `/favorites` | grid of `AcSpaceCard` (client state) |
| `Bookings.razor` | `/bookings` | list of `AcCard` |
| `BookingCreate.razor` | `/book/{id}` | `AcBookingWizardPage` (3 steps) |
| `BookingDetails.razor` | `/booking/{id}` | `AcCard` + cancel button |
| `Host/MySpaces.razor` | `/my-listings` | grid of `AcSpaceCard` |
| `CreateListing.razor` | `/create-listing` | `AcCreateListingPage` |
| `Host/SubscriptionPlans.razor` | `/plans` | grid of plan cards |
| `Host/SubscriptionCheckout.razor` + `PaymentPage.xaml` | `/subscribe` | `AcPaymentCheckoutPage` + `AcPaymentForm` |
| `Host/PaymentCallback.razor` | `/payment/callback` | صفحة محاكاة نتيجة |
| `Chat/Chats.razor` | `/chats` | list |
| `Chat/ChatRoom.razor` | `/chat/{id}` | bubbles with `acs-bubble-mine/.other` |
| `Complaints.razor` + `ComplaintDetails.razor` | `/help` | `AcComplaintsPage` |
| `AppStartup.razor` (force-update) | app-level via `AcVersionGate` | widget يُلفّ ChildContent |

## الودجات الجديدة (في `ACommerce.Widgets`)

- `AcTopNav` — شريط علوي للشاشات ≥768px
- `AcCityPicker` — قائمة مدن للـ hero (8 مدن سعوديّة)
- `AcThemeToggle` — تبديل فاتح/داكن
- `AcLanguageToggle` — تبديل ar/en
- `AcVersionGate` — حارس تحديث (3 حالات: loading/blocked/normal)
- `AcPaymentForm` — نموذج دفع محاكي (بطاقة + تاريخ + CVV + اسم)
- `AcGallery`, `AcStickyActionBar`, `AcMapSim`, `AcSearchListItem` (من الشريحة 3)

## القوالب الجديدة (في `ACommerce.Templates.Marketplace`)

- `AcBookingWizardPage` — 3 خطوات للحجز، تُصدِر `booking.create`
- `AcCreateListingPage` — نموذج إعلان، يُصدِر `listing.create`
- `AcComplaintsPage` — قائمة شكاوى + نموذج إضافة، يُصدِر `complaint.file`
- `AcPaymentCheckoutPage` — ملخّص + نموذج دفع، يُصدِر `payment.charge`

## i18n + dark mode

- `Ashare.V2.Web/Store/L.cs` — قاموس 90+ مفتاح × لغتَين (ar + en) معتمد على `AppStore.Ui.Language`
- `AppStore.SetLanguage`, `SetTheme`, `SetCity` — تغييرات محلّية فقط
- CSS `body.ac-dark` في `widgets.css` يقلب كلّ متغيّرات `--ac-*` إلى قيم داكنة
- Script صغير في `MainLayout` يضبط `dir` و`class` على الـ body حسب AppStore
- `AcThemeToggle` + `AcLanguageToggle` في `AcTopNav.RightActions`

## إصلاح خلل توجيه الأدوات — مشكلة "الأداة تذهب لـ HTTP بدل الانتقال داخل التطبيق"

**السبب**: قوالب Home/Explore/Details تُصدِر `Entry.Create("category.select")`،
`listing.view`، `catalog.filter` عبر `Engine.ExecuteAsync`. الدالة تمرّ عبر
`HttpDispatcher` الذي يرمي `InvalidOperationException: No HTTP route
registered for …` عند عدم تسجيل الـ route — لأن هذه العمليّات UI-only
(تنقّل أو تتبّع) ولا endpoint خلفي لها.

**الإصلاح**:
- عدّلتُ `ClientOpEngine.ExecuteAsync` ليلتقط `InvalidOperationException`
  الخاصّ بـ "No HTTP route registered"، ويعود لتطبيق محلّي (fake envelope
  عبر `IStateApplier.ApplyAsync`) بدل رمي الاستثناء.
- هذا يجعل كل Entry بلا backend → "local-apply fallback" بلا كسر.

**الاختبار**: `scripts/verify-page-structure.sh` قاعدة 10 كانت تصطاد
`SetLanguage/SetTheme/SignOut` الموجَّهَة لـ HTTP — ما زالت فعّالة.
لا حاجة لقاعدة جديدة لأن الـ fallback يعالج الحالة بسلاسة.

## البذر — V1 + V2

**V2** `AshareV2Seed.cs` (توسَّع في هذه الشريحة):
- 10 إعلانات، 5 فئات، 8 مدن، 7 إشعارات
- 4 حجوزات (pending/confirmed/completed/cancelled)
- 2 محادثات × 3 رسائل
- 2 شكاوى، 3 خطط اشتراك
- 7 بحث شائع، 3 quick filters
- 3 مستندات قانونيّة (privacy/terms/refund)
- VersionInfo للحارس

**V1** Ashare.Api (لا يزال موجوداً جنب V2):
- `AshareNotificationsSeed.cs` (أُضيف في الشريحة 3) — 7 × 2 مستخدمَين

## محاكاة الدفع

`AcPaymentCheckoutPage` + `AcPaymentForm`:
- آخر رقم بطاقة زوجيّ → نجاح (ينتقل `/payment/callback?status=success`)
- آخر رقم بطاقة فرديّ → فشل (يُعرَض `AcAlert` داخل النموذج)
- يُصدِر `payment.charge` (Entry) عند النجاح — يُطبَّق محلّياً (لا endpoint).

## فحوص Layer 6 — كلّ الصفحات × حجمين

| Route | Desktop 1366×900 | Mobile 390×844 |
|---|---|---|
| `/`, `/explore`, `/search`, `/space/L-101`, `/notifications` | ✅ 0 | ✅ 0 |
| `/favorites`, `/bookings`, `/booking/B-1`, `/book/L-101` | ✅ 0 | ✅ 0 |
| `/my-listings`, `/create-listing` | ✅ 0 | ✅ 0 |
| `/login`, `/me`, `/settings`, `/legal/privacy` | ✅ 0 | ✅ 0 |
| `/plans`, `/subscribe`, `/payment/callback` | ✅ 0 | ✅ 0 |
| `/chats`, `/chat/C-1`, `/help` | ✅ 0 | ✅ 0 |

**المجموع: 21 route × 2 viewport = 42 فحص — 0 مخالفات.**

## العمليات المُصدَرة (خريطة كاملة)

```
booking.create         /book/{id}     User → Listing  nights, guests, start_date
listing.create         /create-listing User → Listing  category, price, district
listing.favorite       Any page       User → Listing  state=on/off
listing.view           /space/{id}    User → Listing  —
listing.share          /space/{id}    User → Listing  channel=native
catalog.search         /search        User → Catalog  q
catalog.filter         /explore       User → Catalog  category/sort/price…
category.select        /               User → Category —
complaint.file         /help          User → Vendor   subject, severity
payment.charge         /subscribe     User → Vendor   amount, method, currency
ui.set_theme           (local)        User → App      theme
ui.set_language        (local)        User → App      lang
ui.set_city            (local)        User → App      city
app.version.check      MainLayout     App → VersionCh —
```

كلّ عملية بلا HTTP route تُطبَّق محلّياً عبر `IStateApplier` بدل رمي
استثناء.

## المُؤجَّلات المُصرَّح بها

- **Leaflet حقيقي** — `AcMapSim` placeholder لم يُستبدَل بعد.
- **Nafath provider حقيقي** — Login يُحاكي قبول أيّ OTP.
- **DB حقيقيّة لـ V2** — كل البيانات in-memory seed.
- **الأنماط الزخرفيّة السعوديّة (sadu/najdi/etc.)** — CSS placeholders
  مؤجَّلة للشريحة 5.
- **صور حقيقيّة للإعلانات** — تحتاج CDN.
- **ComplaintDetails thread** — الشاشة الحاليّة تعرض القائمة فقط.
- **ProfileEdit، Register، MySubscription، Host/Dashboard** — الوظائف
  موجودة بشكل مختصر في `/me` + `/login` + `/plans`. ستنفصل كـ routes
  في شريحة لاحقة عند ربط DB حقيقيّة.
