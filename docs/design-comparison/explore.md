# Design Comparison — Explore

> عشير القديم: `acommerce-lab/ACommerce.Libraries/Apps/Ashare.Shared/Components/Pages/Explore.razor`
> V2 الجديد: `Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web/Components/Pages/Explore.razor`

## ترتيب الأقسام

1. Filter bar (chips للفئات + زرّ sliders)
2. Sort bar (عدد النتائج + sort dropdown + view toggle)
3. Content: list / map
4. Filter modal (price + capacity + amenities + rating)

**الترتيب مطابق.**

## الجدول التفصيلي

| # | عنصر | عشير القديم (class) | V2 (widget) | ملاحظات الفرق |
|---|---|---|---|---|
| 1 | شريط تصفية أفقي | `.ashare-filter-bar` + `.ashare-filter-chips` | `.acm-explore-filterbar` + `<AcHScroll>` + `<AcFilterChip>` | يُستبدَل كل chip بـ widget |
| 2 | chip "الكل" | `.ashare-filter-chip.active` | `<AcFilterChip Active="true">` | مطابق |
| 3 | chip الفئة | chip + `bi-<icon>` + label | `<AcFilterChip Icon Label>` | أيقونات خطيّة |
| 4 | زرّ فلاتر | `.ashare-filter-button` + `bi-sliders` | `.acm-explore-filterbtn` + `<AcIcon Name="sliders">` | مطابق بصرياً |
| 5 | شريط ترتيب | `.ashare-sort-bar` | `.acm-explore-sortbar` | مطابق |
| 6 | عدد النتائج | `.ashare-results-count` | `.acm-explore-count` "X نتيجة" | مطابق |
| 7 | قائمة ترتيب | `<select class="ashare-sort-select">` | `<AcSortMenu Options>` | مطابق (native select) |
| 8 | تبديل العرض | `.ashare-view-toggle` + btn [list,map] | `<AcViewToggle>` + [list,grid,map] | + خيار grid |
| 9 | شبكة قائمة | `.ashare-spaces-grid` of `.ashare-space-card` | `.acm-explore-grid` of `<AcSpaceCard>` | مطابق |
| 10 | عرض خريطة | `.ashare-explore-map-container` | `.acm-explore-map` + `<AcEmptyState>` placeholder | مُؤجَّل لـ Leaflet لاحقاً |
| 11 | ورقة معاينة | `.ashare-map-preview-sheet` | `<AcBottomSheet>` + `<AcSpaceCard>` | مطابق (حتى الـ handle bar) |
| 12 | مودال الفلاتر | `.ac-modal-overlay` + `.ashare-filter-modal` | `<AcModal Title>` | مطابق (header + body + footer) |
| 13 | زرّ إغلاق | `.ac-modal-close` + `bi-x-lg` | `<AcIcon Name="x">` داخل `.ac-modal-close` | مطابق |
| 14 | نطاق السعر | `.ashare-price-inputs` + 2 inputs | `.acm-explore-pricerange` + 2 `<AcInput Type="number">` | مطابق |
| 15 | بطاقات السعة | `.ashare-capacity-btn` [الكل, 1-5, 6-10, 11-20, 20+] | `<AcFilterChip>` × 5 | نفس الأرقام |
| 16 | المرافق | `.ashare-amenity-check` (checkboxes) | **مُؤجَّل** للشريحة 3 | يحتاج `AcCheckboxChip` |
| 17 | التقييم | `.ashare-rating-btn` [1-5 + `bi-star-fill`] | `<AcFilterChip>` × 5 + star Filled | مطابق |
| 18 | زرّ إعادة | `.ac-btn-secondary` | `<AcButton Variant="ghost">` | استخدمنا ghost لعدم إلهاء بصري |
| 19 | زرّ تطبيق | `.ac-btn-primary` | `<AcButton Variant="primary">` | مطابق |
| 20 | Bottom nav | `.ac-bottom-nav` في MainLayout | `<AcBottomNav>` في MainLayout مع 5 تبويبات | مطابق |

## الفحوص

| Viewport | المسار | Layer 6 | Violations |
|---|---|---|---|
| 1366×900 desktop | `/` | ✅ | 0 |
| 1366×900 desktop | `/explore` | ✅ | 0 |
| 390×844 mobile | `/` | ✅ | 0 |
| 390×844 mobile | `/explore` | ✅ | 0 |

## العمليات المُصدَرة

| تفاعل | Entry type | From | To | Tags |
|---|---|---|---|---|
| اختيار فئة | `catalog.filter` | `User:{id}` | `Catalog:listings` | `category=<id>` |
| تغيير ترتيب | `catalog.filter` | `User:{id}` | `Catalog:listings` | `sort=<value>` |
| تطبيق فلاتر modal | `catalog.filter` | `User:{id}` | `Catalog:listings` | `price_min`, `price_max`, `capacity`, `rating` |
| فتح بطاقة | `listing.view` | `User:{id}` | `Listing:{id}` | — |
| قلب | `listing.favorite` | `User:{id}` | `Listing:{id}` | `state=on/off` |

## الخلافات المقصودة

- **+ خيار grid** في View Toggle (3 بدل 2) — الشبكة مناسبة للسطح المكتبي.
- **Map view placeholder** — Leaflet مُؤجَّل للشريحة 5 (مع الأنماط الزخرفيّة).
- **Amenities checkboxes مُؤجَّلة** — تحتاج `AcCheckboxChip` widget جديد.
- **Reset زرّ ghost بدل secondary** — لأن secondary برتقالي ناعم يلفت أكثر من الإجراء الفعلي (Apply).

## العناصر المُؤجَّلة للشرائح التالية

- خريطة Leaflet حقيقيّة (شريحة 5 مع الأنماط السعوديّة).
- `AcCheckboxChip` للمرافق (شريحة 4 — CreateListing تحتاجه أيضاً).
- بدّل ترتيب "الأحدث" إلى استرداد `createdAt` حقيقي بدل ترتيب seed (شريحة 3 عند ربط DB).
