# Plan — features without 3 uses (Payments, Files, etc.)

> **خطّة لما لا يستوفي Rule of Three بعد** — كيف نُنضِجه بدون قولبة سابقة لأوانها.

## المشكلة

ميزات مثل **الدفع** (موجودة في Ashare V2 فقط)، **التخزين الكائنيّ للملفّات**
(Ashare V2 فقط)، **الحجوزات** (Ashare V2 فقط)، **Nafath 2FA** (Ashare V2 فقط) —
لكلّ منها استعمال واحد. تطبيق Rule of Three يقول: **لا تستخرج Kit بعد**.
لكنّ تركها معطّلة في تطبيق واحد يمنع المنصّات الأخرى من الاستفادة لاحقاً.

## التصنيف

كلّ ميزة منخفضة الاستعمال نضعها في إحدى الفئات الأربع:

### Tier 1 — Operations جاهزة لإعادة الاستعمال، لا Kit

الميزة موجودة كـ `*.Operations.csproj` + `*.Providers.*.csproj` (أنماطنا
المعتادة). التطبيق الواحد يستهلكها مباشرةً. **لا نُنشئ Kit حتّى يأتي
المستهلك الثاني، ونلحظ أيّ تكرار في الـ Controller / UI.**

أمثلة من الموجود:
- `ACommerce.Payments.Operations` + `ACommerce.Payments.Providers.Noon` — مستعمَل في Ashare V2 فقط.
- `ACommerce.Files.Operations` + `ACommerce.Files.Storage.AliyunOSS` + `ACommerce.Files.Storage.Local`.
- `ACommerce.Authentication.TwoFactor.Providers.Nafath.Mock`.

**الإجراء**: **أبقِ الوضع كما هو.** عند ظهور المستهلك الثاني، **افحص**: هل
يكتب نفس الـ Controller؟ هل ينسخ نفس الصفحة؟ إن نعم → ابدأ التحضير لاستخراج
Kit. إن لا → اتركها على Tier 1.

### Tier 2 — استهلاك واحد لكنّ النمط جاهز للقولبة

الميزة عُرفت بالفعل في عدّة منصّات (Spree, Saleor, Magento) كنمط ناضج.
لو استخرجناها كـ Kit الآن مع port واحد بسيط، لن يكون قراراً متعجّلاً.

**الإجراء**: استخرج Kit **خفيفاً** (Operations + Backend Controller +
IPort)، لكن **بدون** Templates أو variants فرعيّة. الصفحات تُكتب يدويّاً
لأوّل تطبيقَين.

ينطبق على:
- **Files / Storage**: `IFileStorage` موجود في `Files.Operations`. الـ
  Controller (POST /uploads + GET /files/{id}) متكرّر-بطبعه. Kit مبكِّر.
- **Payments**: `IPaymentGateway` موجود في `Payments.Operations`. Controller
  لمعالجة الـ webhook + بدء عمليّة الدفع — يصلح Kit.

### Tier 3 — استهلاك واحد، نمطه غير ناضج

الميزة مخصّصة جدّاً لتطبيقها الحاليّ، نمطها غير قياسيّ في الصناعة.

**الإجراء**: **لا تستخرج**. اتركها داخل تطبيقها. وثّق الميزة في PLATFORM.md
الخاصّ بها لتُذكَر مستقبلاً.

ينطبق على:
- **Bookings (Ashare V2)**: نموذج Airbnb-like فيه date-range + guest-count +
  pricing-rules + cancellation-policy. أنماط `Bookings` تختلف جذريّاً بين:
  العقارات (يومي بأسعار موسميّة)، الخدمات (مواعيد ذات سعة محدودة)، الأنشطة
  (شراكات مع جهات خارجيّة). إخراج Kit مبكِّر يُجبر تطبيقات لاحقة على نموذج
  لا يناسبها.
- **Subscriptions الخاصّة بـ Provider plans (Ejar/Ashare V2)**: نموذج Quota
  + billing-cycle لمزوّدي محتوى. لا يطابق نموذج subscription لـ SaaS عميل.

### Tier 4 — استهلاك واحد لخصوصيّة منصّة

ميزة قانونيّة/تنظيميّة لمنصّة واحدة (Nafath السعودي للعقارات/المركبات،
Stripe Connect لمنصّات أمريكيّة، …).

**الإجراء**: **لا تستخرج أبداً**. ابقِ في `*.Providers.*` المخصّص. منطقها
لا يعمم.

## القرار العمليّ الآن

| الميزة | Tier | الإجراء |
|---|---|---|
| Payments (Noon, generic gateway) | 2 | **استخراج Kit خفيف**: `ACommerce.Kits.Payments.Backend` (Controller للـ webhook + بدء الدفع + IPaymentStore port). Templates لاحقاً. |
| Files / Storage | 2 | **استخراج Kit خفيف**: `ACommerce.Kits.Files.Backend` (POST /uploads + GET signed URL + IFileMetadataStore). |
| Bookings | 3 | اترك في Ashare V2. عند المنصّة الثانية، أعد النظر. |
| Subscriptions (Provider quota) | 3 | اترك في Ashare V2 + Ejar. الفرق بينهما يحدّد الـ port المستقبليّ. |
| Nafath 2FA | 4 | اترك مزوّداً منفصلاً. لا Kit. |
| Complaints | 3 | استخراج Kit بعد المستهلك الثالث. حاليّاً Ashare V2 + Ejar فقط. |

## كيف نَعرف أنّ الوقت حان

نوثّق قرار "لمّا" داخل ملفّ الميزة:

```csharp
// libs/backend/sales/ACommerce.Payments.Operations/IPaymentGateway.cs
//
// KIT-EXTRACTION: Tier 2 — Files/Storage تنتظر مستهلكاً ثانياً (e.g. Order V2 idem).
// Trigger: when 2 backends each maintain their own PaymentsController over
// IPaymentGateway. Then move to libs/kits/ACommerce.Kits.Payments.Backend.
```

أيّ مطوّر/agent مستقبليّ يجد الـ trigger مكتوباً ويتّخذ القرار بناءً على
دليل، لا حدس.

## مقاومة الإفراط في القولبة

عند الشكّ، طبّق هذه الأسئلة الثلاثة (من Sandi Metz, *99 Bottles of OOP*):

1. **هل النسختان فعلاً متطابقتان؟** أم أنّ التشابه سطحيّ والاختلافات تنمو؟
2. **هل الـ port سيبقى صغيراً؟** ٣–٧ methods. لو وصل لـ١٥+، الـ Kit يصبح
   إصلاحاً أسوأ من المرض.
3. **هل التطبيقات ستحدّث الإصدار في وقت واحد؟** لو لا، dependency hell
   يُقتل القيمة.

ثلاث "نعم" → استخرج. أيّ "لا" → أجِّل.
