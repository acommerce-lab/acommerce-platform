# دليل ترحيل صفحات عشير القديم إلى `Apps/Ashare.V2`

هذا الملف هو **قائمة تحقّق** لكل صفحة ننقلها من
`acommerce-lab/ACommerce.Libraries/Apps/Ashare.*` (MAUI / Blazor Hybrid)
إلى المنصّة الجديدة. الهدف: تصميم مطابق للإصدار الموبايل مع وعي بالعمليات
والـ operation-accounting.

## المبادئ غير القابلة للتفاوض

تخالف أيًّا منها يُفشل الـ pipeline:

1. **لا `<button>` / `<input>` / `.btn` / `.card` خام** — ودجات `Ac*` فقط.
2. **لا `<i class="bi bi-*">`** — `AcIcon` بخطّ واحد فقط.
3. **لا ألوان hex في `.razor`** — `var(--ac-*)` دائماً.
4. **لا `AshareApiService` ولا خدمات أخرى** — كل مُتغيّر حالة = `Entry.Create(...)`.
5. **كل استجابة خادم = `OperationEnvelope<T>`** (حتى GETs).
6. **mobile-first**: التصميم عند `max-width: 600px` مطابق لعشير القديم.
7. **كل ودجة تمرّ عقد `widget-contracts.json`** (padding + border + min-height).
8. **كل شاشة تمرّ `verify-runtime.mjs` على 390×844 و 1366×900**.

## خطوات ترحيل صفحة واحدة (قالب عام)

لا يوجد اختصار. اتبع كل خطوة بالترتيب.

### 1. جرد العناصر في الصفحة القديمة

افتح الملفّ الأصلي (مثال: `Ashare.Shared/Components/Pages/Explore.razor`)،
واستخرج في جدول:

| عنصر | class القديم | وظيفة |
|---|---|---|
| شريط تصفية | `ashare-filter-bar` | أفقي scrollable مع chips |
| شريط ترتيب | `ashare-sort-bar` | عدّاد نتائج + dropdown |
| بطاقة إعلان | `ashare-space-card` | image + title + meta + price |
| زرّ "مميّز" | `ac-product-badge-featured` | شارة أعلى الصورة |
| … | … | … |

### 2. تصنيف كلّ عنصر

| إذا كان العنصر… | يذهب إلى… |
|---|---|
| ذريّ بلا منطق أعمال (button, input, icon, card, tile) | `libs/frontend/ACommerce.Widgets/Ac*.razor` |
| مركّب متعدّد الحالات (AuthPanel, Chat, Gallery) | `libs/frontend/ACommerce.Templates.Shared/Ac*.razor` |
| خاصّ بالسوق العقاري (Listing, Booking, Host) | `libs/frontend/ACommerce.Templates.Marketplace/Ac*.razor` |
| صفحة كاملة | `Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web/Components/Pages/*.razor` |

**قاعدة**: إذا كان العنصر يُصدِر تغيير حالة (نقر قلب، إرسال نموذج) فهو
قالب operation-aware. إذا كان فقط يُعرض فهو widget ذرّي.

### 3. تحويل الأيقونات

كل `<i class="bi bi-<name>">` ← `<AcIcon Name="<mapped-name>" />`.
الخرائط الشائعة:

| bi-* | AcIcon |
|---|---|
| bi-search | search |
| bi-heart / bi-heart-fill | heart (Filled=true) |
| bi-geo-alt | map-pin |
| bi-people | people |
| bi-star-fill | star (Filled=true) |
| bi-building | building |
| bi-house / bi-house-add | home / house-add |
| bi-chevron-left / right | chevron-left / right |
| bi-bell | bell |
| bi-clock | clock |
| bi-shield-check | shield-check |
| bi-calendar-check | calendar-check |
| bi-x-circle / x-circle-fill | x-circle (Filled=true) |
| bi-check-circle / -fill | check-circle (Filled=true) |
| bi-person / person-circle | user / person-circle |

إذا احتجت أيقونة جديدة أضفها في `libs/frontend/ACommerce.Widgets/AcIcon.razor`
على شكل `"name" => """<path d="..."/>"""` — **خطّ واحد** `stroke=currentColor`
بلا `fill` إلا عند `Filled="true"`. راجع Lucide للحصول على الـ path.

### 4. تحويل الألوان

- **لا hex في `.razor`**. إن لزم، في `wwwroot/app.css` فقط.
- `#1E40AF` → `var(--ac-primary)`
- `#F4844C` أو أيّ برتقالي ← `var(--ac-secondary)` **مع تحقّق contrast**:
  - قيمة `#F4844C` الأصليّة تفشل WCAG AA (2.54:1 مع أبيض).
  - استعمل `#B15215` أو أغمق للنصوص البيضاء فوق برتقالي.
- أنماط سعوديّة (sadu, najdi, roshan, asiri, gypsum): تُنفَّذ SVG stroke
  بـ `currentColor` + `opacity` خفيف، لا تُكتب hex.

### 5. تحويل كل تفاعل إلى عملية

كل onclick أو bind أو submit ← `Entry.Create(...).Execute(...).Build()` ثمّ
`Engine.ExecuteAsync<T>`. أمثلة عمليّة:

| حدث قديم | Entry type | From (debit) | To (credit) | Tags |
|---|---|---|---|---|
| `HandleCategoryClick` | `category.select` | `User:{id}` | `Category:{id}` | — |
| `HandleSpaceClick` | `listing.view` | `User:{id}` | `Listing:{id}` | — |
| نقر قلب | `listing.favorite` | `User:{id}` | `Listing:{id}` | `state=on/off` |
| بحث | `catalog.search` | `User:{id}` | `Catalog:listings` | `q=<query>` |
| بدء حجز | `booking.create` | `User:{id}` | `Listing:{id}` | `nights`, `capacity` |
| دفع | `payment.charge` (sub) | `User:{id}` | `Vendor:{id}` | `amount`, `currency` |

القالب يأخذ `ITemplateEngine` و `ITemplateStore` كـ parameters — لا يحقن
خدمات ولا يعرف التطبيق المستهلك.

### 6. التكيّف مع الموبايل (≤600px)

المنصّة mobile-first. الصفحة تعمل بشكل طبيعي ضمن `max-width: 480px` على
الموبايل. إذا احتاجت صفحة تخطيطًا مختلفًا على سطح المكتب:

```css
/* في templates-marketplace.css أو في app.css للتطبيق */
@media (min-width: 768px) {
    .acm-<page>-grid { grid-template-columns: repeat(3, 1fr); }
    .acm-<page>-rail-item { width: 260px; }
}
```

يجب أن يكون التصميم **في 390×844 مطابقًا لعشير الحالي** حرفياً.
سطح المكتب يمكن أن يوسّع العمود لكن لا يغيّر ترتيب العناصر.

### 7. تشغيل Pipeline الستّ قبل الـ commit

```bash
./scripts/verify-page-structure.sh      # Layer 1 — ممنوع
./scripts/verify-css.sh                 # Layer 2 — ممنوع
./scripts/verify-design-tokens.sh       # Layer 3 — ممنوع
./scripts/verify-design-quality.sh      # Layer 4 — تقرير فقط
./scripts/verify-widget-contracts.sh    # Layer 5 — ممنوع

# Layer 6 (يتطلّب الخادم قيد التشغيل)
dotnet run --no-build --project Apps/Ashare.V2/Customer/Backend/Ashare.V2.Api \
  --urls http://localhost:5600 &
dotnet run --no-build --project Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web \
  --urls http://localhost:5900 &
until curl -sf http://localhost:5900/ >/dev/null; do sleep 1; done

# على سطح المكتب
CHROME_EXEC_PATH=/opt/browsers/chrome-linux64/chrome \
  TARGET_URLS=http://localhost:5900/<route> \
  node scripts/verify-runtime.mjs

# على الموبايل (390×844 iPhone 12)
CHROME_EXEC_PATH=/opt/browsers/chrome-linux64/chrome \
  TARGET_URLS=http://localhost:5900/<route> \
  VIEWPORT=390x844 \
  node scripts/verify-runtime.mjs
```

كل نتيجة يجب أن تكون `0 violations`. أيّ violation يُحلّل ويُصلح (لا
يُتجاهل).

## جدول مقارنة تصميم — قالب مرجعيّ

لكل صفحة مُرحَّلة، أنشئ قسمًا في `docs/design-comparison/<page>.md`
أو في هذا الملف نفسه. البنية:

```markdown
## Home

| عنصر | عشير القديم (class) | V2 (widget) | ملاحظات |
|---|---|---|---|
| لافتة علويّة | `.ashare-hero-branded` | `<AcHeroBanner>` | خلفيّة var(--ac-primary) |
| شعار | `.ashare-hero-logo` | `LogoContent` slot | دائرة أبيض شفّاف |
| عنوان + فرعي | `.ashare-hero-title/.subtitle` | Title + Subtitle params | — |
| بحث | `.ac-search-box` (readonly) | `<AcSearchBox>` active | active on V2 |
| فئات | `.ashare-category-item` | `<AcCategoryTile>` | دائرة أيقونة 56px |
| CTA | `.ashare-cta-branded` | `<AcCtaCard Variant="secondary">` | `#B15215` (WCAG) |
| بطاقة | `.ashare-space-card` | `<AcSpaceCard>` | meta: capacity + rating |
| Quick action | `.ashare-action-card` | `<AcActionCard>` | primary / secondary |
| ترتيب الأقسام | Hero → Categories → CTA → Featured → New → Actions | مطابق | — |
```

## قائمة الصفحات المتبقّية

الحالة `✅` = مُرحَّلة وتمرّ كل الفحوص. `⏳` = قيد العمل. `⬜` = لم تُبدأ.

| # | صفحة قديمة | مسار جديد | حالة |
|---|---|---|---|
| 1 | Home | `/` | ✅ |
| 2 | Explore (قائمة) | `/explore` | ✅ |
| 2b | Map view داخل Explore | `/explore?view=map` (client state) | ✅ (محاكاة CSS + دبابيس) |
| 2c | Search suggestions page | `/search` | ✅ |
| 3 | SpaceDetails | `/space/{id}` | ✅ |
| 4 | BookingCreate + BookingDetails | `/book/{id}`, `/booking/{id}` | ⬜ |
| 5 | Bookings (list) | `/bookings` | ⬜ |
| 6 | Favorites | `/favorites` | ⬜ |
| 7 | Notifications | `/notifications` | ✅ |
| 8 | Profile + ProfileEdit | `/me` | ⬜ |
| 9 | Settings / Language | `/settings` | ⬜ |
| 10 | Login (Nafath) | `/login` | ⬜ |
| 11 | Register | `/register` | ⬜ |
| 12 | Chats + ChatRoom | `/chats`, `/chat/{id}` | ⬜ |
| 13 | Complaints + ComplaintDetails | `/help`, `/help/{id}` | ⬜ |
| 14 | LegalPageView | `/legal/{key}` | ⬜ |
| 15 | CreateListing | `/create-listing` | ⬜ |
| 16 | MySpaces / OwnerBookings | `/host/*` | ⬜ |
| 17 | SubscriptionPlans / Checkout / Dashboard | `/plans`, `/subscription` | ⬜ |
| 18 | Version gate (AppStartup) | app-level guard | ⬜ |

## مرجع: خريطة عمليات عشير

كل الـ Entry types المتوقّعة مع From/To حسب المجال:

```
auth.nafath.request   User           IdentityProvider:nafath  channel=nafath
auth.nafath.verify    User           IdentityProvider:nafath  otp=<rn>
auth.sms.request      User           IdentityProvider:sms     phone=<n>
auth.sms.verify       User           IdentityProvider:sms     code=<c>
auth.sign_out         User           App:ashare               —

listing.view          User           Listing:{id}             —
listing.favorite      User           Listing:{id}             state=on|off
listing.create        User           Listing:{id}             category, price
listing.update        User           Listing:{id}             —
listing.draft.save    User           DraftStore:{user}        —
listing.draft.resume  DraftStore     User                     —

category.select       User           Category:{id}            —
catalog.search        User           Catalog:listings         q=<query>
catalog.filter        User           Catalog:listings         price_min, price_max, amenities…

booking.create (parent) User         Listing:{id}             nights, capacity
  ├─ booking.validate_dates  ─ sub
  ├─ booking.hold_room       ─ sub
  └─ payment.charge          ─ sub (requires IPaymentGateway)

complaint.file        User           Vendor:{id}              severity, category
complaint.reply       Author         Complaint:{id}           —

subscription.subscribe User          Plan:{slug}              requires IPaymentGateway
subscription.cancel   User           Subscription:{id}        reason

tracking.consent.grant  User         TrackingConsentStore     —
tracking.consent.revoke User         TrackingConsentStore     —

app.version.check     App            VersionChannel           requires IAppVersionChannel
app.version.block     App            User                     current, latest, store_url
```

كل من هذه العمليات يُسجَّل تلقائيًا في `journal_entries` (Phase 0.2)،
ولوحة الأدمن تستعلم عنها عبر `IAccountQuery.GetPartiesAsync(identity)`.

## المواضع التي لا تُرحَّل مباشرة

| عنصر قديم | السبب | البديل |
|---|---|---|
| `AshareApiService` | خدمة مركّزة تخالف القانون 1 | `ClientOpEngine` + `Entry.Create` لكل endpoint |
| `ThemeService`, `LocalizationService` | حالة كخدمة | `AppStore.Ui` + Interpreters |
| `PendingListingService` | خدمة في طبقة UI | interceptor على `listing.create` + `IDraftStore` provider contract |
| `IAppNavigationService` | تجريد غير ضروري | `NavigationManager` مباشرة |
| `LazyImage` | منطق تحميل خاص | `<img loading="lazy">` على `AcSpaceCard` |
| `<iframe sandbox="...">` في LegalPageView | — | يُحتفَظ به داخل `AcLegalPageView` — هذا استثناء مُبرَّر |

## ختام

**لا تبدأ صفحة جديدة قبل أن تكون الصفحة السابقة:**
1. تمرّ الفحوص الستّ بدون مخالفة.
2. موثَّقة في قسم "جدول مقارنة تصميم".
3. عملياتها مُسجَّلة في "خريطة عمليات عشير".
4. حالتها `✅` في جدول "الصفحات المتبقّية".

هذه الوثيقة هي الـ single source of truth لحالة الترحيل.
