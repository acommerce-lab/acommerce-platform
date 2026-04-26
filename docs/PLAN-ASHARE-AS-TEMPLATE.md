# Plan — turn Ashare V2 into a reusable platform template

> **خطّة تحويل Ashare V2 إلى مكتبة قالب** يمكن تخصيصها لخدمة Ashare وEjar
> معاً (وأيّ كتالوج عقاريّ أو سوق إعلانات لاحقاً) عبر استبدال Domain +
> ملف بوت ستراب.

## لماذا Ashare V2 وليس غيرها

- **الأوسع**: تحوي كلّ الأنماط الموجودة (catalog، chat، bookings، complaints،
  subscriptions، payments، files، notifications، 2FA).
- **الأنضج**: مرّت بأكثر إعادة هيكلة (Half 1 + Half 2 من Law 6، Provider/Admin
  منفصلة، Domain extracted).
- **Ejar نسخة منها مصغَّرة**: لو أمكن تشكيل القالب بحيث يُنتج Ejar كمجرّد
  toggle off لـ Bookings + Payments على Ashare، فالقالب أثبت نفسه.

## ما هو "القالب" بالضبط

**ليس Razor template** ولا "starter kit يُنسخ ويُعدَّل". القالب = مكتبة
runtime واحدة، تستوردها التطبيقات، تتغذّى من ملفّ بوت ستراب C#.

```
ACommerce.Platforms.RealEstateMarketplace/      ← مكتبة قالب
  ├── PlatformBootstrap.cs                       ← UseRealEstateMarketplace<TDom>(opts)
  ├── PlatformOptions.cs                         ← الميزات + brand + JWT + ports
  ├── DefaultStores/                             ← stores قياسيّة فوق Domain interface
  ├── DefaultPolicies/                           ← interceptors قياسيّة (Quota, Ownership, …)
  └── ACommerce.Platforms.RealEstateMarketplace.csproj   ← يجلب كلّ Kits المطلوبة
```

تطبيق Ashare V2 مستقبلاً يصبح:

```csharp
// Apps/Ashare.V2/Customer/Backend/Ashare.V2.Api/Program.cs
using ACommerce.Platforms.RealEstateMarketplace;
using Ashare.V2.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseRealEstateMarketplace<AshareDomainAdapter>(opts =>
{
    opts.Role = PlatformRole.Customer;
    opts.Brand.AppName  = "عشير";
    opts.Brand.Primary  = "#0E7C66";
    opts.Features.Bookings      = true;
    opts.Features.Payments.Noon = true;
    opts.Features.Files.AliyunOss = true;
    opts.Jwt = new(...);
    opts.Cors = new[] { "https://ashare.app" };
});

var app = builder.Build();
app.UseRealEstateMarketplace();
app.Run();
```

تطبيق Ejar Customer الجديد:

```csharp
using ACommerce.Platforms.RealEstateMarketplace;
using Ejar.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseRealEstateMarketplace<EjarDomainAdapter>(opts =>
{
    opts.Role = PlatformRole.Customer;
    opts.Brand.AppName = "إيجار";
    opts.Brand.Primary = "#1B4FE6";
    opts.Features.Bookings = false;          // ← إيجار كتالوج فقط
    opts.Features.Payments = null;           // ← لا دفع داخل التطبيق
    opts.Features.Files.Local = true;        // ← Aliyun ليس مطلوباً
    opts.Jwt = new(...);
});

var app = builder.Build();
app.UseRealEstateMarketplace();
app.Run();
```

**هذا "متجر يعمل في دقائق" بـ ٢٠ سطراً + Domain library**.

## التحدّيات الفنّيّة

### ١. عقد الـ Domain (الـ adapter)

القالب يحتاج "interface adapter" يُترجِم بين **عَقده الموحَّد** (`IListing`،
`IBooking`، `IUserProfile`) وبين كيانات Ashare/Ejar الفعليّة.

```csharp
namespace ACommerce.Platforms.RealEstateMarketplace.Domain;

public interface IPlatformDomainAdapter
{
    // Stores يحتاجها كلّ Kit مفعَّل
    IListingStore   Listings { get; }
    IBookingStore?  Bookings { get; }      // null إن toggled off
    IPaymentStore?  Payments { get; }
    IChatStore      Chat     { get; }
    IAuthUserStore  Users    { get; }
    INotificationStore Notifications { get; }
    // …
}
```

كلّ منصّة تكتب adapter يربط هذه الواجهات بـ EjarSeed أو AshareV2Seed أو
EF DbContext. كم سطر؟ ~٢٠٠ سطر للأشمل (Ashare)، ~١٠٠ للأبسط (Ejar).

### ٢. تركيب الـ Razor Pages

القالب يحوي صفحات Razor قياسيّة (`Home`، `Catalog`، `Listing details`،
`Chat`، `Profile`، `Settings`). التطبيق يحقن:
- **brand**: ألوان CSS vars + شعار + اسم.
- **navigation**: ترتيب الـ nav items + أيّ منها يُخفى.
- **page overrides**: إن أراد التطبيق صفحة `Home` مخصّصة، يضع `Home.razor`
  في تطبيقه — Blazor route resolution يُفضّل الأقرب.

### ٣. ملفّ التهيئة هو **C#** لا JSON

كما طلبتَ في الجولة السابقة. السبب الجوهريّ:
- type-safety (نسيان feature toggle = compile error، لا runtime).
- IntelliSense على `opts.Features.Payments.Noon`.
- إمكانيّة كتابة منطق شرطيّ ("لو dev، فعّل Mock SMS، لو prod، Real").
- يحتفظ بإمكانيّة تشغيل أجزاء على hosts مختلفة (MAUI، Workers، API).

```csharp
opts.Features.Payments = builder.Environment.IsProduction()
    ? new NoonPaymentsConfig(returnUrl: ...)
    : new MockPaymentsConfig();
```

## المراحل الزمنيّة

### المرحلة T-0 (شرط مسبق) — أنجزت

- [x] Domain extracted لـ ٣ منصّات.
- [x] ٣ Kits (Chat, Auth, Notifications) في الإنتاج.
- [x] Ejar Provider+Admin migrated to kits.

### المرحلة T-1 — اكتمال الـ Kits قبل القالب

قبل أن نعمم Ashare كقالب، يجب أن تكون الـ Kits التالية موجودة:
- [x] Chat
- [x] Auth.Sms
- [x] Notifications
- [ ] **Catalog** ← مطلوب (يفصل listings عن business specifics)
- [ ] **Files** ← مطلوب (لو القالب يدعم رفع الصور)
- [ ] **Payments** ← مطلوب لو القالب يدعم الدفع داخل التطبيق
- [ ] Bookings ← مطلوب فقط لو القالب feature-toggled

تدوير الـ Catalog Kit أوّل. بدونه القالب ينقصه الجوهر.

### المرحلة T-2 — استخراج `IPlatformDomainAdapter`

نقل الـ Stores الموجودة الآن في كلّ تطبيق إلى `IPlatformDomainAdapter`
موحَّد. التطبيق يكتب نسخة واحدة بدلاً من ٣-٤ stores منفصلة.

### المرحلة T-3 — إنشاء `ACommerce.Platforms.RealEstateMarketplace`

- بنية مكتبة Class library.
- `PlatformOptions` كاملة.
- `UseRealEstateMarketplace<T>()` extension يحقن: Database، JWT، Kits،
  Razor pages، realtime، cache.
- `app.UseRealEstateMarketplace()` يربط: middleware، Hub، endpoints.

### المرحلة T-4 — قياس النجاح

تطبيق ثالث جديد (مثلاً منصّة محلّات تجاريّة `OurMarket`) يُكتب بـ:
- Domain library جديدة (~٢٠٠ سطر).
- Adapter (~١٢٠ سطر).
- Program.cs قالب (~٢٥ سطر).
- brand assets.

إجماليّ ~٤٠٠ سطر للحصول على متجر متعدّد البائعين عامل. **لو وصلنا هذا الرقم،
نجحنا.**

## مخاطر متوقّعة

| الخطر | التخفيف |
|---|---|
| القالب يفترض أنّ Bookings تشبه نموذج Ashare؛ منصّة جديدة لها نموذج مختلف | الـ adapter يكشف `IBookingStore` كاملاً، لكن منطق الـ Controller داخل القالب يُغلَف بـ feature flag. لو Bookings off، الـ Controller لا يُسجَّل. |
| `IPlatformDomainAdapter` يصبح god-interface (٣٠+ method) | قسّم بـ partial interfaces مفعَّلة بحسب feature flags: `IPlatformWithBookings : IPlatformDomainAdapter, IBookingStore`. |
| القالب صعب الترقية لأنّه يلمس كلّ شيء | monoversion + breaking changes في minor versions موثَّقة في CHANGELOG. التطبيقات تترقّى متى استطاعت. |
| الـ Razor pages المُضمَّنة لا تُشبه الـ brand | اسمح بـ overrides عبر Blazor's route precedence. أي صفحة في التطبيق تطغى على الصفحة في القالب. |

## القرار

**لا نبدأ T-3 الآن**. نسبق بالخطوات:

1. **الموجة القادمة** (إن واصلنا): إكمال المراحل A+B+C من
   `PLAN-PLATFORM-SLIMMING.md` لجعل كلّ الـ ٧ تطبيقات على Kits.
2. **بعدها**: استخراج Catalog Kit.
3. **بعدها**: تجريب IPlatformDomainAdapter في تطبيق واحد (Ejar Customer).
4. **بعدها فقط**: بدء `ACommerce.Platforms.RealEstateMarketplace`.

ترتيب لا يحرق المراحل. كلّ مرحلة تتأكّد من سلامة الأساس قبل البناء فوقه.
