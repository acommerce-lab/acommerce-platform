# دورة حياة الإصدارات مع النشر

> "كيف أتحكّم بتحريك الإصدارات مع النشر؟" — هذا الملفّ.

## النموذج

| الحالة          | معنى للخادم                | معنى للعميل (واجهة Razor)                            |
|-----------------|----------------------------|------------------------------------------------------|
| `Latest`        | اقبل الطلب                 | لا تنبيه — هذا أحدث ما لديك                           |
| `Active`        | اقبل الطلب                 | يمكن إظهار info banner لو وُجد Latest أحدث             |
| `NearSunset`    | اقبل الطلب                 | شريط تحذير: «الدعم سينتهي قريباً»                     |
| `Deprecated`    | اقبل الطلب                 | شريط تحذير قويّ: «إصدارك ملغى الدعم»                  |
| `Unsupported`   | **ارفض الطلب** (interceptor) | شاشة كاملة: «حدّث للمتابعة» (`AcAppVersionGate`)       |

## السياسة عند إصدار **غير مسجَّل** في DB

اختياران في `VersionGateOptions.UnknownVersionPolicy`:

| السياسة     | السلوك                                              | متى تستخدم                                       |
|-------------|----------------------------------------------------|--------------------------------------------------|
| `Lenient` (الافتراضيّ) | يُعامَل كـ `Active` → يُسمح بالطلب           | المرحلة الأولى — لا تعرف ماذا في الميدان         |
| `Strict`    | يُعامَل كـ `Unsupported` → يُحجَب الطلب           | بعد نضوج النظام وتسجيل كلّ الإصدارات الموجودة     |

التبديل في `Program.cs`:
```csharp
builder.Services.AddVersionsKit<EjarVersionStore>(new VersionGateOptions
{
    UnknownVersionPolicy = UnknownVersionPolicy.Strict   // عند النضوج
});
```

## مكان تخزين الإصدارات

جدول `AppVersions` في DB، عبر `AppVersionEntity` و `EjarVersionStore` المدعوم بـ EF Core. الفهرس uniques على `(Platform, Version)` يمنع التكرار.

## البذرة الأوّليّة

`DbInitializer.SeedAppVersionsIfMissing(db)` تُستدعى عند كلّ بدء تشغيل (idempotent). تضيف فقط الإصدارات غير الموجودة:

```csharp
("web",    "1.0.0", Latest, "https://ejar.ye/download")
("wasm",   "1.0.0", Latest, "https://ejar.ye/download")
("mobile", "1.0.0", Latest, "https://ejar.ye/download/mobile")
("admin",  "1.0.0", Latest, "https://ejar.ye/download/admin")
```

تعمل البذرة على قاعدة البيانات الجديدة وعلى القديمة معاً.

## الدورة العملية لكلّ نشر

افترض أنّ الإصدار الحاليّ في الميدان هو `1.0.0` Latest، وأنت تنشر `1.1.0`.

### الخطوات

1. **في الكود** (الواجهة):
   - في `Ejar.Web/Program.cs` و `Ejar.WebAssembly/Program.cs` ضع:
     ```csharp
     var appVersion = builder.Configuration["App:Version"] ?? "1.1.0";
     ```
   - أو الأفضل: ضع `"App:Version": "1.1.0"` في `appsettings.json` لكلّ host.
   - تأكّد أنّ `AppVersionInfo` المسجَّل يستخدم القيمة هذه.

2. **في DB** (قبل أو مع النشر):
   - عبر `POST /admin/versions` ↔ شاشة `AcAdminVersionsPage`، أضف:
     ```json
     { "platform": "web",  "version": "1.1.0", "status": 0,  "downloadUrl": "..." }
     { "platform": "wasm", "version": "1.1.0", "status": 0 }
     ```
     (الحالة `0 = Latest`)
   - بدّل حالة `1.0.0` على نفس المنصّات إلى `Active` (`1`):
     ```
     POST /admin/versions/web/1.0.0/status   { "status": 1 }
     POST /admin/versions/wasm/1.0.0/status  { "status": 1 }
     ```
   - النتيجة: الإصدار الجديد هو الأحدث، القديم لا يزال يعمل لكن بلا "Latest" (يظهر info banner للمستخدم).

3. **انشر الكود**.

### عندما تريد إجبار المستخدمين القدامى على التحديث

بعد فترة نضوج (أسبوع/شهر…)، حوّل `1.0.0` إلى:
- `Deprecated` → الواجهة تظهر شريط تحذير قويّ، لكنّ التطبيق يعمل.
- `NearSunset` (مع `sunsetAt`) → عدّ تنازليّ في الواجهة.
- `Unsupported` → الـ Interceptor يرفض كلّ طلب من 1.0.0 + الواجهة تعرض شاشة "حدّث".

كلّ ذلك بضربة واحدة على `/admin/versions/web/1.0.0/status` بحالة جديدة. لا تعديل في الكود.

## التشديد التدريجيّ

في البداية: `Lenient` + لا تسجّل الإصدارات القديمة → كلّ شيء يعمل.

كلّما عرفت إصداراً قديماً في الميدان: سجّله بحالة مناسبة عبر `/admin/versions`.

عندما تتأكّد أنّك سجّلت كلّ إصدار قديم في الميدان: بدّل إلى `Strict` في `Program.cs`. الآن أيّ إصدار غير موجود في DB يُحجَب — حماية إضافيّة ضدّ العملاء العشوائيّين.

## ماذا لو احتاج عميل قديم endpoint غير موجود؟

طبقتان:

1. **Versions Kit** يحجب على مستوى الإصدار الكامل (Unsupported = حجب كلّ المسارات).
2. **Route Map per version** (مذكور في `docs/API-DRIFT-PREVENTION.md` الحلّ ٢.٦) — لو كان عندك مسار قديم تُريد إعادة توجيهه لمسار جديد دون حجب الإصدار كلّه.

## ملاحظة على المنصّات (`Platform`)

القيمة تأتي من رأس `X-App-Platform` الذي يضعه الـ frontend. الاتفاق الحاليّ:
- `web` — Ejar.Web (Blazor Server)
- `wasm` — Ejar.WebAssembly (Blazor WASM)
- `mobile` — Ejar.Maui
- `admin` — لاحقاً (Admin app)

أيّ منصّة جديدة → أضف seed entry لها في `DbInitializer.SeedAppVersionsIfMissing` + سطر تسجيل في `Program.cs` للـ `AppVersionInfo`.
