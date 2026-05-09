# منع انحراف عقد الـ API بين الواجهة والخدمة (API Drift Prevention)

> "اختفت نقطة نهاية كان التطبيق يعتمد عليها / تغيّر مسارها بصمت / تغيّر شكل
> الـ payload" — هذه أنماط متكرّرة عند بناء منصّات متعدّدة الواجهات
> (Web/WASM/MAUI/iOS/Android) وخدمات خلفيّة تتطوّر مستقلّةً عنها. هذا
> الوثيقة تسرد الحلول المعروفة ومتى يُستخدَم كلٌّ منها، ثمّ توصي بخطّة
> ملموسة لإيجار.

## 1) ما الذي حدث في إيجار الآن

- Razor pages تستدعي `Api.GetAsync("/me/profile")` لكنّ الـ backend ينشر المسار
  على `/api/catalog/me`. النتيجة: 404.
- `EjarRoutes` (مُجمَّع داخل المكتبة المشتركة) يربط `complaint.file → POST /complaints`؛
  الـ backend ينشر `/api/support`. 404.
- مسار `version/check` تكرّر بإصدارَين (واحد في `HomeController` وواحد في
  `VersionsKit`). 200 من أحدهما، 404 من الآخر بحسب ترتيب التسجيل.

السبب الجذريّ: **العقد ضمنيّ**. الواجهة تعرف ما تنادي به، الخدمة تعرف ما
تنشره، ولا أحد يفرض المطابقة.

## 2) الحلول الشائعة (مرتّبة من الأبسط للأكثر أثراً)

### 2.1 ✅ **عقد مكتوب** (Contract document) — هذا ما فعلناه للتوّ

ملفّ مرجعيّ واحد يُسرَد فيه كلّ مسار + verb + body + envelope shape. مثاله
عندنا الآن: `docs/EJAR-API-CONTRACT.md`.

**متى يكفي**: مع فريق صغير، CI خفيف، تطبيق واحد.

**حدوده**: غير قابل للتنفيذ آلياً — ينحرف بصمت ما لم تُجبر مراجعة الـ PR.

### 2.2 **اختبارات تكامل عقديّة** (Contract Tests)

اختبارات على الـ backend تتحقّق أنّ كلّ مسار في العقد يستجيب بـ ٢٠٠ (أو ٤٠١
لو محميّ) بشكل End-to-End. تُجرى في الـ CI على كلّ PR.

**عملياً للإيجار**: ملفّ `tests/Ejar.ApiContract.Tests/RouteSmokeTests.cs`
يستخدم `WebApplicationFactory<Program>` ويرسل طلبات على كلّ مسار من
`docs/EJAR-API-CONTRACT.md` ويتأكّد ألاّ يُرجع `404`.

### 2.3 **Source-of-truth واحد لقائمة المسارات**

بدل تكرار الثوابت، يُعرَّف المسار مرّة واحدة في مكتبة مشتركة، يستهلكه الجانبان.
نمطنا الحاليّ في `EjarRoutes` يبدأ من هنا — لكنّه ناقص (يحوي بعض المسارات لا
كلّها). التوسعة:

```csharp
// libs/kits/Ejar.Contract (مكتبة مشتركة بين الـ frontend والـ backend)
public static class EjarApi
{
    public const string MeProfile      = "/me/profile";
    public const string MyListings     = "/my-listings";
    public const string Notifications  = "/notifications";
    // ...
}
```

الـ Razor يستهلكها: `Api.GetAsync(EjarApi.MeProfile)`.
الـ Controller يستهلكها: `[HttpGet(EjarApi.MeProfile)]`.

**حد**ّ: يتطلّب إعادة البناء عند كلّ تغيير عقد، ويُلزم الـ MAUI binary بإطلاق
جديد (لا يصل تغيير المسار إلى الجهاز مباشرةً).

### 2.4 **OpenAPI / Swagger** (Schema Generation)

مولّد متاح في `Ejar.Api` (تستخدم بالفعل `AddSwaggerGen`). إضافة بسيطة:
- نولّد `swagger.json` في كلّ build.
- نضيف خطوة CI تتحقّق أنّ الـ paths فيه تتطابق مع `EjarRoutes` ومع الاستدعاءات
  في `Ejar.Customer.UI` (regex grep على `Api.GetAsync\("([^"]+)"`).

أيّ مسار في الواجهة لا يظهر في `swagger.json` = فشل CI.

### 2.5 **توليد عميل من المخطّط** (Generated Client)

الـ frontend لا يكتب `Api.GetAsync("/me/profile")` يدوياً — يستهلك عميلاً
مولّداً من `swagger.json` (مثلاً `NSwag` أو `Refit + Swashbuckle`):

```csharp
// مولّد، لا يُكتب يدوياً
public partial interface IEjarApi
{
    [Get("/me/profile")] Task<OperationEnvelope<ProfileDto>> GetMeProfileAsync();
}
```

عند تغيير مسار في الخدمة → يفشل البناء في الواجهة على الفور بدل اكتشاف
المشكلة في 404 وقت التشغيل.

**العائق في إيجار**: الواجهة تستهلك `OperationEnvelope` الذي يضمّ تاج عمليّة،
ليس فقط DTO. لكنّ `NSwag` يدعم هذا عبر `AdditionalContractGenerators`.

### 2.6 **خرائط نقاط نهاية لكلّ إصدار** (Version-Scoped Route Maps) — ما اقترحه المستخدم

العميل يُرسل `X-App-Version` في كلّ طلب (متوفّر بالفعل عبر `AppVersionHeadersHandler`).
الـ backend يحتفظ بـ "خرائط مسارات" في الذاكرة (أو ملفّ JSON):

```json
// data/route-maps/web/1.0.0.json
{
  "/me/profile":              "/me/profile",
  "/api/legacy/old-listings": "/listings"
}

// data/route-maps/web/1.1.0.json
{
  "/me/profile":              "/me/profile",
  "/me/billing":              "/me/subscription"
}
```

عند ورود الطلب:
1. middleware يقرأ `X-App-Version`، يحمّل خريطة العميل.
2. لو المسار الوارد له تطابق مختلف في الخريطة → rewrite شفّاف.
3. الخدمة تتعامل مع المسار الـ canonical فقط.

**الفائدة**: تطبيق MAUI شُحن بمسار قديم؟ لا مشكلة — الخريطة تترجمه. الخدمة
لا تتلوّث بـ `if (version < 1.1) ... else ...`.

**العيب**: بعض overhead لكلّ طلب. مناسب للـ web/mobile-grade traffic لكن
ليس للـ hot-path الفائق الأداء.

### 2.7 **العميل يطلب خريطته مرّة عند الإقلاع** (Bootstrap Manifest)

مكمّل لـ 2.6: العميل يستدعي `GET /api/manifest?version=1.0.0&platform=web`
عند بدء التشغيل ويستلم:

```json
{
  "endpoints": {
    "MeProfile":     { "method": "GET",  "path": "/me/profile" },
    "MyListings":    { "method": "GET",  "path": "/my-listings" },
    "ListingToggle": { "method": "POST", "path": "/my-listings/{id}/toggle" }
  },
  "features": { "chat": true, "subscriptions": true }
}
```

العميل يخزّن الـ manifest محليّاً ويستخدمه طوال الجلسة. عند تغيير الخادم
لمسار، يكفي تحديث الـ manifest — لا تحديث للتطبيق على الجهاز.

**الفائدة**: تكلفة طلب واحدة في كلّ بدء جلسة (≈ كاش يوميّ)، يدعم اختفاء
ميزات (feature flags)، يدعم A/B testing.

**العيب**: أعقد قليلاً في التنفيذ، يتطلّب طبقة generation للـ manifest
على الخادم.

### 2.8 **GraphQL / TypedQuery** (Schema-First)

الواجهة تطلب الحقول التي تحتاجها لا غير، الخادم يقدّمها. التغيير في الشكل
لا يكسر العميل ما دامت الحقول الأساسيّة باقية.

**العائق**: تحويل كبير، يخالف مبدأ Operation/Envelope الحاليّ. غير مقترح
لإيجار في المرحلة الحاليّة.

### 2.9 **API Gateway مع Path Aliasing**

طبقة بين العميل والخدمة (مثل YARP، Envoy، أو Caddy) تترجم المسارات:

```yaml
- match: /me/profile
  rewrite: /api/catalog/me
- match: /complaints
  rewrite: /api/support
```

**الفائدة**: لا تعديل في الكود (لا في الواجهة ولا في الكتلب الكتل). الـ DevOps
يديرها.

**العيب**: قطعة بنية تحتيّة إضافيّة، نقطة فشل، غير شفّافة لمن يقرأ الكود.

### 2.10 **Consumer-Driven Contracts** (Pact أو ما يشبهه)

كلّ مستهلك (Web, WASM, MAUI) يكتب توقعاته كاختبار. الخادم يتأكّد في CI أنّ
كلّ المستهلكين راضون قبل أن يُنشر.

**العائق**: أداة منفصلة (Pact Broker)، تعقيد إضافيّ. مفيد عندما يتعاقد
**فريقَ نِطاقَيْن** مختلفان (frontend team vs. backend team).

## 3) خطّة موصى بها لإيجار

### المرحلة الأولى — الآن (هذا الـ PR)
1. ✅ توثيق `docs/EJAR-API-CONTRACT.md` (مرجع الفريق).
2. ✅ تنظيف مسارات `Ejar.Api` لتطابق العقد (هذا PR).
3. ✅ وحدة `Versions Kit` فاعلة + `X-App-Version` يُرسل من الواجهة.

### المرحلة الثانية — قريباً (PR صغير)
4. **اختبارات smoke routing**: `tests/Ejar.ApiContract.Tests` تُرسل GET على
   كلّ مسار في العقد عبر `WebApplicationFactory` وتفشل لو ٤٠٤. ربط بـ CI.
5. **مكتبة عقديّة مشتركة**: `libs/Ejar.Contract` يحوي ثوابت المسارات +
   DTOs الـ envelope. الـ Razor والـ Controllers يستهلكانها.

### المرحلة الثالثة — متوسطة المدى (شهر تقريباً)
6. **Bootstrap Manifest** (الحلّ 2.7): العميل يستدعي `GET /api/manifest`
   مرّة عند بدء الجلسة، يخزّن قائمة المسارات و feature flags. الخدمة تولّدها
   من نفس مكتبة العقد. يحلّ مشكلة شحن binary قديم في MAUI.
7. **OpenAPI generation + check**: خطوة CI تطابق Swagger مع الـ frontend
   call sites.

### المرحلة الرابعة — اختياريّ مستقبليّ
8. **Version-scoped route maps** (الحلّ 2.6) لو ظهر سيناريو هجرة كبيرة.
9. **Generated client** (الحلّ 2.5) لو وصل العقد إلى ١٠٠+ نقطة وصارت
   صيانة الثوابت يدويّاً مرهقة.

## 4) خلاصة

| المشكلة                                  | الحلّ الأنسب لإيجار                          |
|------------------------------------------|---------------------------------------------|
| اكتشاف انحراف بصمت في PR                  | اختبارات smoke routing (٢.٢) في CI          |
| كتابة المسار مرّتين                        | مكتبة عقديّة مشتركة (٢.٣)                    |
| MAUI binary شحن وما عاد يطابق الخادم      | Bootstrap Manifest (٢.٧)                     |
| migrate كبيرة بدون كسر العملاء القدامى     | Version-scoped route maps (٢.٦)              |
| أتمتة كاملة على الـ frontend             | Generated client من OpenAPI (٢.٥)            |

التوصية: ابدأ بـ ٢.١ + ٢.٢ + ٢.٣ — تكلفتها أيّام، تكسب ٩٠٪ من الفائدة. أضف
٢.٧ عندما يبدأ العمل على MAUI app stores.
