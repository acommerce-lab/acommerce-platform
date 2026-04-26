# Plan — slim existing & new platforms toward template-readiness

> **خطّة لتحويل المنصّات إلى أنحف ما يمكن** — استهلاك Kits + Domain فقط،
> الكود التطبيقيّ يبقى للحصصيّة الفعليّة فقط.

## الوضع الحاليّ (بعد wave 3)

| تطبيق | حالة Domain | حالة Kits | Auth lines | Chat lines | الإجمال |
|---|---|---|---:|---:|---:|
| Ejar Customer | ✅ Domain لكن لا Kits | hand-rolled | ~150 | ~200 | كثير |
| Ejar Provider | ✅ Domain + Auth/Chat Kits | ✅ | 0 (kit) | 0 (kit) | **~70 سطر store** |
| Ejar Admin | ✅ Domain + Auth Kit | جزئيّ | 0 (kit) | n/a | ~12 سطر store |
| Ashare V2 Customer | ✅ Domain | hand-rolled | ~120 | ~250 | كثير |
| Ashare V2 Admin | ✅ Domain | hand-rolled | ~120 | ~150 | متوسّط |
| Order V2 Customer | ✅ Domain | hand-rolled | ~120 | ~170 | كثير |
| Order V2 Vendor | ✅ Domain | hand-rolled | ~80 | ~150 | متوسّط |
| Order V2 Admin | ✅ Domain | hand-rolled | ~120 | n/a | متوسّط |

## الهدف

كلّ تطبيق يصبح:
```
Apps/{Platform}/{Role}/Backend/
  ├── {Service}.csproj         ← ProjectRef: {Platform}.Domain + Kits.* مطلوبة
  ├── Program.cs               ← ~80 سطر: Database + JWT + Kits + Build
  ├── Stores/                  ← ~10–60 سطر/store يربط Kit بـ EjarSeed/EF
  ├── Controllers/             ← فقط ما لا يقدّمه أيّ Kit (admin tooling…)
  └── appsettings*.json
```

التطبيق المتوسّط = **ملف Program.cs + بضعة Stores** = ~200 سطر إجماليّ بدلاً
من 800–1000 حاليّاً.

## المراحل

### المرحلة A — استكمال Auth Kit في كلّ التطبيقات

**الإجراء الميكانيكيّ** (مطابق لما فعلناه في Ejar Provider/Admin):

لكلّ تطبيق غير مرحَّل:
1. أضف ProjectReference إلى `ACommerce.Kits.Auth.Sms`.
2. أنشئ `Stores/{Platform}{Role}AuthUserStore.cs` (~12 سطر).
3. في Program.cs: استبدل `AddSingleton<{X}JwtConfig>` + `AddMockSmsTwoFactor`
   بـ `AddSmsAuthKit<...>(jwt)`.
4. احذف `Controllers/AuthController.cs`.
5. احذف Record `{X}JwtConfig` من Program.cs إن كان موجوداً.

**زمن متوقّع/تطبيق**: ٢-٣ دقائق. **لـ ٧ تطبيقات متبقّية**: ~٢٠ دقيقة.

### المرحلة B — استكمال Chat Kit حيث ينطبق

التطبيقات التي لها MessagesController حاليّاً:
- Ejar Customer (CatalogController يحوي chat endpoints — استخرج).
- Order V2 Customer (MessagesController مستقلّ — استبدل).
- Order V2 Vendor (VendorMessagesController — استبدل).
- Ashare V2 Customer (CatalogController يحوي chat — استخرج).

لكلّ منها:
1. ProjectReference إلى `ACommerce.Kits.Chat`.
2. أنشئ `Stores/{X}ChatStore.cs` (5 methods، ~50–80 سطر).
3. في Program.cs: `AddChatKit<...>(o => o.PartyKind = "User")`.
4. احذف الـ chat endpoints من الـ Controllers.

**زمن متوقّع/تطبيق**: ٥-٨ دقائق.

### المرحلة C — Notifications Kit حيث ينطبق

كلّ تطبيق لديه `NotificationsController` أو endpoints ضمن CatalogController:
1. ProjectReference إلى `ACommerce.Kits.Notifications`.
2. أنشئ `Stores/{X}NotificationStore.cs` (~30 سطر).
3. `AddNotificationsKit<...>()`.
4. احذف الـ notification endpoints القديمة.

### المرحلة D — استخراج Kits إضافيّة على الطلب

تتبّع الـ Rule of Three. عند ظهور تكرار جديد (Catalog/Listings تكرّر الآن
في Ejar Customer + Provider — قارب الحدّ):
1. اقرأ `docs/KIT-PATTERN.md`.
2. ابنِ Kit جديد.
3. ارجع للمرحلة A للتطبيقات الجاهزة لاستهلاكه.

### المرحلة E — Frontend Kits (لاحقاً)

ما زلنا نُهجّر الـ Backends. الـ Frontend kits (Razor templates) ستأتي
بعد استقرار الـ Backend kits، وسيكون لها Razor class library + Pages قابلة
للتركيب بـ tag واحد. النموذج الأوّل:
`ACommerce.Kits.Chat.Templates` (موجود).

## النتيجة المتوقَّعة

بعد المراحل A+B+C على ٧ تطبيقات:

```
خطوط الكود المُحذوفة:    ~3500
خطوط الـ Stores المُضافة:  ~700
صافي:                       -2800 سطر
```

كلّ خطّ مكرَّر اليوم مكتوب مرّة واحدة في `libs/kits/`. تحديث/إصلاح/ميزة
جديدة في الدردشة = تعديل مكتبة واحدة، يستفيد منها كلّ التطبيقات على الترقية
التالية.

## مخاطر التهجير + التخفيف

| الخطر | التخفيف |
|---|---|
| كسر تطبيق منشور بسبب اختلاف بسيط في الـ kit (مثلاً مسار مختلف) | كلّ تطبيق يُهجَّر في commit مستقلّ، يُختبر يدويّاً قبل الانتقال للتالي. |
| الـ Kit ينقصه ميزة كانت في النسخة الأصليّة | افحص الـ MessagesController/AuthController الأصليّ قبل الحذف. لو ينقص شيء، أضف للـ Kit أوّلاً. |
| Migrating apps before kit is mature | المرحلة 3 الحاليّة على Ejar Provider/Admin هي الـ canary. شغّلهما، اختبرهما، ثمّ هجّر بقيّة التطبيقات. |
| Drift بين Kit و حالة الإنتاج | monoversion (نفس الإصدار للـ kit عبر كلّ التطبيقات في الـ monorepo). |

## خارطة طريق محتمَلة (إن قُرِّر التنفيذ)

| الموجة | المحتوى | زمن متوقّع |
|---:|---|---|
| 5 | Auth Kit في ٧ تطبيقات + قراءة سريعة | يوم |
| 6 | Chat Kit في ٤ تطبيقات | يوم |
| 7 | Notifications Kit حيث ينطبق | نصف يوم |
| 8 | Catalog Kit (استخراج جديد) + ٢ مستهلكَين | يوم-يومان |
| 9 | Files Kit (استخراج جديد) + ١-٢ مستهلكَين | يوم |
| 10 | Payments Kit (استخراج جديد) + Ashare V2 | يوم |

عند نهاية الموجة 10: التطبيق الجديد أكبر من ~250 سطر يكون **ميزة فعليّة**،
ليس مجرّد سَكفولد.

## شرط ضروريّ قبل البدء

تشغيل Ejar Provider + Admin (المُهاجَران في commit `3bad3e3`) فعليّاً
والتأكّد من عمل Auth + Chat كاملَين قبل تعميم النمط. هذه canary deployment
بقياس لأهليّة الـ Kits.
