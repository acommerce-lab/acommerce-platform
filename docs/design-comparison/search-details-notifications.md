# Design Comparison — Search / SpaceDetails / Notifications / Responsive Nav

هذه الصفحات والتعديلات المتعلّقة بتنقّلات الشريحة الثالثة (الثلاثة دفعة واحدة
كما طُلِب).

## Search suggestions page

> عشير القديم: `Apps/Ashare.Shared/Components/Pages/Search.razor`
> V2: `Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web/Components/Pages/Search.razor`

### ترتيب الأقسام

1. مربّع البحث (AcSearchBox)
2. Quick filters (قريب مني / الأقل سعراً / الأعلى تقييماً)
3. عمليات البحث الأخيرة + "مسح الكل"
4. عمليات بحث شائعة (chips)

**الترتيب مطابق.**

### الجدول التفصيلي

| عنصر | عشير القديم | V2 | ملاحظات |
|---|---|---|---|
| Search box | readonly → يفتح صفحة بحث | AcSearchBox تفاعلي + يُصدِر `catalog.search` | تحسّن |
| Quick filters | `.ashare-quick-filter` × 3 | `<AcFilterChip>` × 3 | Active state محفوظ في AppStore |
| Section header | `.ashare-section-header` + زرّ "مسح الكل" | `.acs-search-section-head` + `<button>` | مطابق |
| Recent item | `.ashare-recent-item` + clock-history + x | `<AcSearchListItem>` + clock + Removable | مطابق |
| Popular tags | `.ashare-popular-tag` chip | `<AcFilterChip>` | مطابق |
| حالة فارغة | (لا توجد) | `<AcEmptyState>` | ترقية UX |

### العمليات المُصدَرة

- `catalog.search` عند submit — أو عند نقر اقتراح أخير/شائع.
- التخزين المحلي في `AppStore.RecentSearches` (10 آخر فريدة).
- `search.recent.remove` + `search.recent.clear` مستقبليّة — حاليًا محليّة فقط.

## SpaceDetails

> عشير القديم: `Apps/Ashare.Shared/Components/Pages/SpaceDetails.razor`
> V2: `Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web/Components/Pages/SpaceDetails.razor`

### ترتيب الأقسام

1. معرض صور (gallery) + overlay (عودة / مشاركة / قلب)
2. عنوان + موقع + meta (الأشخاص / التقييم)
3. بطاقة المالك
4. الوصف
5. قائمة المرافق
6. شريط ثابت أسفل الشاشة (سعر + "احجز الآن")

**الترتيب مطابق.**

### الجدول التفصيلي

| عنصر | عشير القديم | V2 | ملاحظات |
|---|---|---|---|
| Gallery main | `.ashare-gallery-main` + img | `<AcGallery>` — صور + أسهم + إبهامات | widget جديد بعرض 16:10 |
| Back / share / heart | أزرار دائرية فوق الصورة | `OverlayContent` + `.acm-details-circle-btn` | نفس الشكل، `currentColor` |
| Title | `.ashare-space-title` | `.acm-details-title` | مطابق |
| Location | `.ashare-space-location-large` | `.acm-details-location` + AcIcon map-pin | مطابق |
| Owner card | `.ashare-owner-card` + avatar | `.acm-details-owner` + دائرة AcIcon user | لا صورة (يحدَّث عند ربط DB) |
| Description | نصّ فقرات | `.acm-details-description` | مطابق |
| Amenities | `.ashare-amenities-list` + bi-* | `.acm-details-amenities` grid + AcIcon map | 5 أيقونات معتمدة (wifi/ac/kitchen/parking/laundry) |
| Booking bar | `.ashare-booking-bar` ثابت sticky | `<AcStickyActionBar>` — سعر + زرّ احجز | sticky على الموبايل، عادي على السطح المكتبي |

### العمليات المُصدَرة

- `listing.view` (تلقائياً عند تحميل البيانات)
- `listing.favorite` state=on|off
- `listing.share` channel=native
- `booking.start` listingId

### المُؤجَّلات

- صور حقيقيّة (الـ seed لا يحوي thumbnails حتى ربط CDN)
- Booking wizard في `/book/{id}` (شريحة 4)

## Notifications

> عشير القديم: `Apps/Ashare.Shared/Components/Pages/Notifications.razor`
> V2 يستفيد من `AcNotificationsPage` الجاهز في Templates.Shared.

### الجدول التفصيلي

| عنصر | عشير القديم | V2 | ملاحظات |
|---|---|---|---|
| قائمة إشعارات | `.ashare-notifications-list` | `<AcNotificationsPage>` | widget جاهز في Shared |
| إشعار واحد | icon + title + body + time | NotificationRowDto → AcNotificationRow | نفس البنية |
| Mark all read | زرّ علوي | `OnMarkAllRead` callback | مطابق |
| حالة read/unread | lightBlue bg للغير مقروء | افتراضي AcNotificationRow | مطابق |

### بذر الإشعارات — للإصدارين

**V2** (in-memory): `Apps/Ashare.V2/Customer/Backend/Ashare.V2.Api/Services/AshareV2Seed.cs`
— 7 إشعارات عيّنة (حجوزات، رسائل، عروض جديدة، ترقيات).

**V1** (DB): `Apps/Ashare/Customer/Backend/Ashare.Api/Services/AshareNotificationsSeed.cs`
— مُضاف إلى `AshareSeeder.SeedAsync` ليبذر 7 إشعارات × مستخدمَين
(OwnerAhmed, CustomerSara). يعتمد مبدأ "لا يبذر إن وُجدت بيانات".

## التنقّل المتجاوب

### قبل

- MainLayout: bottom-nav دائمًا مرئي، لا top-nav.

### بعد

- **موبايل (≤767px)**: bottom-nav 5 تبويبات (الرئيسية / استكشف / المفضلة / حجوزاتي / حسابي). `.ac-topnav` مخفي بـ `display: none`.
- **سطح مكتب (≥768px)**: top-nav 6 تبويبات (+ الإشعارات). `.ac-topnav` يُظهَر بـ `display: flex` في media query. bottom-nav يُخفى.

**الأساس**: `.ac-topnav` في widgets.css يبدأ `display: none`. App-level CSS
في `Ashare.V2.Web/wwwroot/app.css` يُظهره على `≥768px`. Layer 6 يمرّ على
الحجمين لأنّ:
- mobile: AcBottomNav موجود ومرئي (`.acs-bottom-nav`).
- desktop: AcTopNav موجود ومرئي (`.ac-topnav`).

### روابط النقر الجديدة

| حدث | قبل | بعد |
|---|---|---|
| نقر مربّع بحث في Home | stays on Home | يفتح `/search` |
| نقر فئة في Home | stays on Home | يفتح `/explore?category={id}` |
| نقر بطاقة إعلان | stays on page | يفتح `/space/{id}` |
| نقر "عرض الكل" | stays on page | يفتح `/explore` |
| Quick action "ابحث عن شريك سكن" | — | يفتح `/explore?category=shared` |
| نقر تبويب الإشعارات (top-nav) | — | يفتح `/notifications` |

## البذر من الإصدار الأول

تبعًا لطلبك، بذور V2 مقتبسة من `AshareListingsSeed.cs` القديم:
- 10 إعلانات (أحياء حقيقيّة: النرجس، الملقا، العارض، غرناطة، العزيزية، المزاحمية…).
- lat/lng حقيقيّة — تعمل على AcMapSim مع bounds تلقائيّة.
- 5 فئات (apartment, room, studio, villa, shared) — مبسّطة مقارنة بالـ V1 (Residential, LookingFor…).

بذر الإشعارات: 7 أنواع × 2 مستخدمين للـ V1، و7 للـ V2. نفس النصوص العربيّة.

## فحوص Layer 6

| Route | Desktop 1366×900 | Mobile 390×844 |
|---|---|---|
| `/` | ✅ 0 | ✅ 0 |
| `/explore` | ✅ 0 | ✅ 0 |
| `/search` | ✅ 0 | ✅ 0 |
| `/space/L-101` | ✅ 0 | ✅ 0 |
| `/notifications` | ✅ 0 | ✅ 0 |

**10/10 صفحة × حجم — 0 مخالفات.**
