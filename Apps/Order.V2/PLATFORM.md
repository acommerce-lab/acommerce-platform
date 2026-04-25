# Order V2 — منصّة عروض الكافيهات والمطاعم

> **اقرأ أوّلاً**: `docs/MODEL.md` (نموذج العمليّات OAM)، `docs/ARCHITECTURE.md`،
> `CLAUDE.md` (القوانين السبعة). هذه الوثيقة تشرح **هذه المنصّة الفرعيّة بعينها**،
> لا الإطار العام.

## ما هي

تطبيق متعدّد التجّار لـ **عروض اليوم** من الكافيهات والمطاعم — العميل يطّلع
على عروض المتاجر القريبة، يضيف للسلّة، ويأتي للاستلام (داخل المتجر أو من
السيارة Curbside). **الدفع يتمّ نقداً أو ببطاقة عند الاستلام — لا دفع
إلكترونيّ في الخدمة الحاليّة.**

## التطبيقات الفرعيّة

ثلاثة تطبيقات Blazor Server منفصلة تشترك في الـ Domain libraries:

| التطبيق | الجمهور | المسار | الهدف |
|---|---|---|---|
| `Customer/Frontend/Order.V2.Web` | المستهلك النهائيّ | الويب (موبايل أوّلاً) | تصفّح العروض، السلّة، إكمال الطلب، تتبّعه |
| `Vendor/Frontend/Order.V2.Vendor.Web` | صاحب المتجر | الويب | إدارة العروض، استقبال الطلبات، تأكيدها/تجهيزها |
| `Admin/Frontend/Order.V2.Admin.Web` | إدارة المنصّة | الويب | لوحة تحكّم، إدارة المستخدمين والمتاجر والتصنيفات |

كل تطبيق Frontend يقابله Backend خاصّ:

| الخدمة | المنفذ | الجمهور |
|---|---|---|
| `Customer/Backend/Order.V2.Api` | (انظر appsettings) | جميع نقاط نهاية المستهلك |
| `Vendor/Backend/Order.V2.Vendor.Api` | (انظر appsettings) | عمليّات صاحب المتجر |
| `Admin/Backend/Order.V2.Admin.Api` | (انظر appsettings) | عمليّات المسؤول |

## كيف يعمل (الدفق التشغيليّ)

```
العميل                              الـ Backend                    التاجر
────────                            ─────────                       ──────
  │ 1. يتصفّح العروض                                                │
  │ ─────────────────────────────► [GET /catalog]                  │
  │ 2. يضيف للسلّة (محليّاً)                                          │
  │ 3. يكمل الطلب                                                  │
  │ ─────────────────────────────► [POST /orders/create]           │
  │                                  ينشئ Operation                │
  │                                  «order.create»                │
  │                                  ────────────► realtime push   │
  │                                                       ────────►│
  │                                                                │ 4. يقبل
  │                                  ◄──────── [POST /orders/{}/accept]
  │                                                                │
  │                                                                │ 5. يجهّز
  │                                  ◄──────── [POST /orders/{}/ready]
  │ 6. يستلم ويدفع                                                  │
  │ ◄────────── يصل الإشعار realtime                               │
  │                                                                │ 7. يسلّم
  │                                  ◄──────── [POST /orders/{}/deliver]
```

## القوانين الخاصّة بهذه المنصّة

بالإضافة إلى **القوانين السبعة العامّة** في `CLAUDE.md`:

### 1. الدفع عند الاستلام فقط

لا تُضِف بوّابات دفع إلكترونيّة (Stripe/Noon/HyperPay) في هذا الإصدار.
نموذج `PaymentMethod` يقتصر على `Cash` و `Card` ويُسجَّل وقت الاستلام
بمعرفة التاجر — لا يمرّ بالخادم كعمليّة دفع. هذا تصميم متعمَّد، ليس قصور.

### 2. الاستلام له نوعان فقط

`PickupType ∈ { InStore, Curbside }`. عند `Curbside` تُطلب بيانات السيارة
(موديل، لون، لوحة اختياريّة). أيّ نوع استلام آخر (delivery/shipping) يُرفض
في الـ analyzer قبل أن تصل العمليّة للـ engine.

### 3. الـ Realtime عبر التجريد

كل تواصل لحظيّ بين الخدمات (إشعار طلب جديد للتاجر، تأكيد للعميل) يمرّ عبر
`ACommerce.Realtime.Operations` و `ACommerce.Realtime.Client.*` —
**ممنوع** استدعاء `IHubContext` أو `Microsoft.AspNetCore.SignalR`
مباشرةً من أيّ Controller أو صفحة. مزوّد SignalR يُحقَن في `Program.cs`
فقط.

### 4. الترجمة عبر `.resx`

كل صفحة Razor تستخدم `@inject L L` ثمّ `L["key"]` — والمفاتيح في
`Resources/Strings.resx` (إنجليزيّ افتراضيّ) و `Resources/Strings.ar.resx`
(عربيّ). راجع `docs/I18N.md`.

## كيف يجب أن يعمل (الحالة المرجوّة)

- **Customer** هو **المرجع الرسميّ** لتطبيق Blazor مترجم بالكامل وفق
  المعمارية (Law 7). إذا أردتَ نمطاً لتطبيق جديد فابدأ من هنا.
- **Vendor** و **Admin** يجب أن يساووا Customer في:
  - استخدام `L["key"]` فقط — لا `L.T("ع","en")` متناثرة.
  - حقن المخزون عبر `AppStore` و `AppLangContext`.
  - تسجيل الإشعارات اللحظيّة عبر التجريد، لا SignalR مباشر.
- جميع نقاط نهاية الـ Backend ترجع `OperationEnvelope` (Law 2) — حتّى نقاط
  القراءة. المخالفات تُكشف بـ `grep -nE 'return Ok\(' Controllers/*.cs` —
  يجب ألّا تُرجع شيئاً.
- ملفّات `appsettings.Development.json` يجب أن تحوي `"Urls": "http://localhost:XXXX"`
  لتجنّب فخّ إيقاع المنفذ على 5000 (راجع Rule T5 في `CLAUDE.md`).

## مخطّط الكيانات (مختصر)

```
User ──< Order >── Vendor
              │
              ├── OrderItem >── Offer
              ├── PickupDetails (InStore | Curbside { CarModel, CarColor, CarPlate? })
              └── PaymentDetails (Cash | Card, AmountTendered?)

Conversation ──< Message
       │
       └─ References Order? Offer?
```

كل تحوّل حالة (`Pending → Accepted → Ready → Delivered → Done | Cancelled`)
هو **عمليّة واحدة** بمرسِل ومستقبِل وعلامات وعقد تنفيذ — بلا استثناء.

## مراجع داخل الريبو

- `Apps/Order.V2/Customer/Frontend/Order.V2.Web/Store/L.cs` — قالب L10n.
- `Apps/Order.V2/Customer/Backend/Order.V2.Api/Controllers/OrdersController.cs`
  — قالب Controllers ملتزم بـ LAW 2.
- `docs/BUILDING-A-BACKEND.md` و `docs/BUILDING-A-FRONTEND.md` — الوصفات.
