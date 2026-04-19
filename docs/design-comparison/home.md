# Design Comparison — Home

> عشير القديم: `acommerce-lab/ACommerce.Libraries/Apps/Ashare.Shared/Components/Pages/Home.razor`
> V2 الجديد: `Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web/Components/Pages/Home.razor`

## ترتيب الأقسام

1. Hero branded (logo + title + subtitle + search)
2. Categories (horizontal rail)
3. CTA "لديك غرفة للمشاركة؟"
4. Featured spaces (horizontal rail)
5. New spaces (grid)
6. Quick actions (2 cards)

**الترتيب مطابق بالكامل.**

## الجدول التفصيلي

| # | عنصر | عشير القديم (class) | V2 (widget) | ملاحظات الفرق |
|---|---|---|---|---|
| 1 | لافتة علويّة | `.ashare-hero.ashare-hero-branded` | `<AcHeroBanner>` | خلفيّة `var(--ac-primary)` = `#1E40AF` مطابق |
| 2 | نمط زخرفي على اللافتة | `.ashare-hero-pattern` | — | **مُؤجَّل** للشريحة 5 (الأنماط الخمسة) |
| 3 | شعار | `<img src="ashare-logo.png" height=80>` | `LogoContent` slot + `<AcIcon Name="home">` داخل دائرة بيضاء | حالياً أيقونة — تُستبدَل بـ PNG عند تنزيل الشعار |
| 4 | عنوان | `.ashare-hero-title` "ابحث عن سكنك المشترك" | `Title` parameter | **مطابق نصًّا ولونًا وحجمًا** (18px أبيض) |
| 5 | فرعي | `.ashare-hero-subtitle` | `Subtitle` parameter | "تطبيق السكن المشترك الأول في السعودية" |
| 6 | صندوق بحث | `.ac-search-box` **readonly** ينقل إلى `/search` | `<AcSearchBox>` **تفاعلي** يُصدِر `catalog.search` | V2 يبحث inline بدل الانتقال — ترقية لا تراجع |
| 7 | عنوان قسم | `.ac-section-header` | `<AcSectionHeader>` | مطابق |
| 8 | بطاقة فئة | `.ashare-category-item` | `<AcCategoryTile>` | دائرة أيقونة 56px بخلفيّة `color-mix(primary, 12%)` — مطابق |
| 9 | أيقونة الفئة | `<i class="bi bi-<icon>">` + inline `color: <hex>` | `Icon=` parameter → `<AcIcon>` | كلّها ترث `var(--ac-primary)` — توحيد بلا تراجع |
| 10 | CTA Section | `.ashare-cta-branded` | `<AcCtaCard Variant="secondary">` | `#B15215` (بدلاً من `#F4844C`) للـ WCAG AA |
| 11 | أيقونة CTA | `bi-house-add` | `IconName="house-add"` → دائرة `rgba(255,255,255,0.2)` | مطابق بصرياً |
| 12 | سهم CTA | `bi-chevron-left.ashare-cta-arrow` | `ShowArrow=true` → `chevron-left` | مطابق |
| 13 | شبكة Featured | `.ac-products-scroll` | `<AcHScroll Class="acm-home-rail">` | عرض بطاقة 220px (موبايل 180px) |
| 14 | بطاقة إعلان | `.ashare-space-card` | `<AcSpaceCard>` | الصورة + العنوان + الموقع + meta + سعر |
| 15 | meta row | `.ashare-space-meta` (capacity + rating) | داخل `<AcSpaceCard>` → أيقونة `user` + `star` (Filled) | أُضيفت بعد ملاحظة المستخدم |
| 16 | شارة | `.ac-product-badge-featured/new` "مميّز/جديد" | `Ribbon=` parameter | مطابق |
| 17 | قسم New | `.ac-section` + `.ac-products-scroll` | `<AcSectionHeader>` + `.acm-home-grid` | في V2 شبكة بدل scroll — مناسب للـ "أُضيف حديثاً" |
| 18 | Quick Actions | `.ashare-action-primary/secondary` | `<AcActionCard Variant="primary/secondary">` | شبكة `repeat(2, 1fr)` |

## الخلافات المقصودة (ليست مخالفات)

- **البحث تفاعلي في V2** — القديم يحوّل إلى صفحة `/search`، V2 يُصدِر
  `catalog.search` مباشرة.
- **البرتقالي `#B15215` بدل `#F4844C`** — للامتثال لـ WCAG AA 4.5:1.
  القديم يخالف (2.54:1).
- **أيقونات خط واحد (`AcIcon`) بدل Bootstrap Icons** — مبدأ التصميم في
  `DESIGN-CRITERIA.md`.
- **الشبكة (grid) للـ New بدل scroll أفقي** — مناسب أكثر للـ desktop
  دون كسر الموبايل (على الموبايل الشبكة تصبح عمودين).

## فحوص التوافق الحاليّة

| Viewport | Layer 6 | Violations |
|---|---|---|
| 1366×900 (desktop) | ✅ | 0 |
| 390×844 (iPhone 12) | ✅ | 0 |

## العناصر المُؤجَّلة للشرائح التالية

- `.ashare-hero-pattern` — يحتاج widget pattern بـ SVG stroke-only (شريحة 5).
- شعار PNG — يحتاج تنزيل `ashare-logo.png` من `Ashare.App/Components/Pages`
  المستودع القديم ووضعه في `wwwroot/images/` (شريحة 5).
- Bottom navigation (5 عناصر) — شريحة 2 (Explore) عند إضافة التبويبات.
