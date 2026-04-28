# إيجار Ejar — كتالوج عقاريّ مع دردشة

> **اقرأ أوّلاً**: `docs/MODEL.md` (نموذج العمليّات OAM)، `docs/ARCHITECTURE.md`،
> `CLAUDE.md` (القوانين السبعة). هذه الوثيقة تشرح **هذه المنصّة الفرعيّة بعينها**.

## ما هي

تطبيق **كتالوج عقاريّ** للإيجار — مالك العقار يضع إعلاناً (شقّة، فيلا،
محلّ، مكتب…) ويختار وحدة الزمن المناسبة (يوميّ/شهريّ/سنويّ)، والمستأجر
يبحث ويفلتر ويتواصل مباشرةً مع المالك عبر **الدردشة في الزمن الحقيقيّ**
لإكمال الإيجار خارج التطبيق.

> **مهمّ — لا حجوزات في هذا الإصدار**. إيجار **ليس** سوق حجز عبر التطبيق
> (لا `POST /bookings`، لا تأكيد دفع، لا تقييم بعد الإقامة). الإيجار يتمّ
> ميدانيّاً بين المالك والمستأجر بعد تواصلهما عبر الدردشة. هذا تصميم
> متعمَّد للمرحلة الحاليّة، ليس قصور.

## التطبيقات الفرعيّة

تطبيق Customer واحد فقط (لا Provider/Admin مستقلّ — المالك يستخدم نفس
تطبيق Customer مع دور `landlord`).

| التطبيق | المسار |
|---|---|
| Frontend (Customer) | `Customer/Frontend/Ejar.Web` |
| Backend | `Customer/Backend/Ejar.Api` |

## كيف يعمل (الدفق التشغيليّ)

```
المالك                            الـ Backend                     المستأجر
──────                            ─────────                       ────────
  │ 1. يشترك في خطّة                                                │
  │ ─────────────────► [POST /me/subscription]                    │
  │                       يفعّل حدّ النشر                            │
  │ 2. يضيف إعلاناً                                                  │
  │ ─────────────────► [POST /my-listings]                        │
  │                       فحص الحدّ → إنشاء → نشر                    │
  │                                                                │ 3. يبحث/يفلتر
  │                                  ◄──────── [GET /listings?...]
  │                                                                │ 4. تفاصيل الإعلان
  │                                  ◄──────── [GET /listings/{id}]
  │                                                                │ 5. يضيف للمفضلة
  │                                  ◄──────── [POST /listings/{}/favorite]
  │                                                                │ 6. يبدأ محادثة
  │  ◄── realtime push ──────── ── [POST /conversations/start]
  │ 7. يردّ                                                         │
  │ ─────────────────► [POST /conversations/{id}/messages]        │
  │                       بثّ realtime ─────────────────────────►  │ 8. يستلم
  │ 9. يتّفقان خارج التطبيق على المعاينة والإيجار                   │
```

## القوانين الخاصّة بهذه المنصّة

بالإضافة إلى **القوانين السبعة العامّة** في `CLAUDE.md`:

### 1. لا حجوزات داخل التطبيق

ممنوع إضافة `BookingsController` أو نقطة نهاية `/bookings`. إن طلب
المنتج هذا التحوّل لاحقاً، يكون عمليّة منفصلة بقرار صريح — ليس انجراف
ميزة.

### 2. النشر مرتبط بالاشتراك

كلّ `POST /my-listings` يمرّ عبر `ListingQuotaInterceptor` الذي يقارن
عدد الإعلانات النشطة بحدّ الخطّة المشترَك بها. لا التفاف.

### 3. الدفع للاشتراكات فقط

المدفوعات الوحيدة في إيجار هي **اشتراك المالك في الخطّة**. لا توجد
معاملات دفع بين المستأجر والمالك تمرّ بالمنصّة. مزوّد الدفع يُحقَن في
`Program.cs` ويُستهلَك حصراً عبر تجريد `IPaymentGateway`.

### 4. الدردشة هي الـ Path الحرج

لأنّ كلّ صفقة تبدأ بدردشة، فإنّ تجربة الدردشة في الزمن الحقيقيّ هي
**خاصّيّة المنتج الأساسيّة** — لا "ميزة جانبيّة". القواعد:

- **لا SignalR مباشر** في أيّ صفحة Razor أو Controller. كلّ شيء عبر
  `ACommerce.Chat.Client.*` (Frontend) و `ACommerce.Realtime.Operations`
  (Backend).
- عند فتح المحادثة: قناة دردشة (Chat Channel) لهذه المحادثة تُفتح،
  والـ Backend يُسكت قناة الإشعارات لها.
- عند إغلاق المحادثة (زرّ الرجوع، إغلاق التطبيق، خمول): قناة الإشعارات
  لتلك المحادثة تُعاد تشغيلها.
- الرسائل الواردة أثناء فتح المحادثة تُعرض داخلها فقط (لا إشعار مكرَّر).
- الرسائل الواردة بعد الخروج تظهر إشعاراً داخل التطبيق (وإشعار Push
  لاحقاً عند ربط Firebase).

تفاصيل البروتوكول في وثيقة الدردشة `docs/CHAT-PROTOCOL.md` (قيد الإنشاء).

### 5. التحقّق بالنفاذ اختياريّ، لا إلزاميّ

على عكس Ashare V2، إيجار **لا يُلزم** المالك بالنفاذ — يكتفي بـ SMS OTP.
سبب التصميم: الكتالوج العقاريّ يستفيد من الانتشار، ولا يَمسّ التحقّقُ
ميدانيّاً جوهرَ القيمة. متى أدخلنا حجوزات أو دفعاً، يصبح النفاذ إلزاميّاً.

### 6. Firebase Push عند الإصدار للمتاجر

تجربة المحمول تتطلّب إشعارات Push (Android/iOS). يُحقَن مزوّد
`ACommerce.Notification.Providers.Firebase` في `Program.cs` ويُستهلَك
عبر تجريد `INotificationChannel`. **في التطوير**: مزوّد InApp فقط
(داخل التطبيق). **في الإنتاج**: InApp + Firebase معاً.

## كيف يجب أن يعمل (الحالة المرجوّة)

- **Frontend**:
  - كلّ نصّ ظاهر يمرّ بـ `L["key"]` من `Resources/Strings.resx` /
    `Strings.ar.resx` (Law 7). ✅ الحالة الحاليّة.
  - كلّ صفحة دردشة (`ChatRoom.razor`) تستخدم `ACommerce.Chat.Client`
    وتشترك في قناة المحادثة فقط. عند مغادرة الصفحة (Dispose) تُغلق
    القناة فوراً.
  - متغيّر "المحادثة النشِطة" يعيش في `AppStore.Ui.ActiveConversationId`
    ويُمسَح بإغلاق التطبيق (BeforeUnload) لإعادة الإشعارات.
- **Backend**:
  - كلّ نقطة نهاية ترجع `OperationEnvelope` (Law 2). ✅ الحالة الحاليّة.
  - كلّ بثّ لحظيّ عبر `ACommerce.Realtime.Operations` — مزوّد SignalR
    يُحقَن في `Program.cs` فقط.
  - الإشعارات تمرّ بـ `INotificationChannel`؛ تكتم إشعارات المحادثة
    تلقائيّاً متى كان المستلم مشتركاً في قناة دردشتها (التنسيق منوط
    بالـ realtime abstraction، لا بكود الإشعارات).
- **قاعدة البيانات**: SQL Server في الإنتاج (راجع `appsettings.json`
  → `Database.ConnectionString`). التطوير يقبل `Provider: "sqlite"` مع
  ملفّ محلّيّ.

## مخطّط الكيانات (مختصر)

```
Profile
  ├── PhoneNumber, Role (renter|landlord)
  └── Subscription >── Plan
                    └── Invoice (>=0)

Listing (مملوك لـ Profile)
  ├── Title, Description
  ├── Category (apartment|villa|shop|office|...)
  ├── Amenities[], City, District, GeoPoint
  ├── Price, TimeUnit (Hourly|Daily|Monthly|Yearly)
  ├── Photos[] (IFileStorage)
  ├── Status (Draft|Active|Paused|Removed)
  └── Views, FavoriteCount

Favorite (User × Listing)

Conversation
  ├── ParticipantPartyIds[]   ← المستأجر + المالك
  ├── ListingId               ← الإعلان الذي بُدِئت منه
  └── Message[] (Sender, Body, SentAt, ReadBy[])

Notification (per User)
  ├── Type (NewMessage|ListingView|ComplaintReply|...)
  ├── ConversationId? / ListingId? / ComplaintId?
  └── ReadAt?

Complaint
  ├── Subject, Body, Status (Open|InReview|Resolved|Closed)
  └── Reply[]
```

## ما الذي **لا** يجب فعله في إيجار

- إضافة `Booking` (تصميماً).
- إضافة دفع بين مستأجر ومالك عبر التطبيق.
- استدعاء `IHubContext` مباشرة في Controller أو الصفحة.
- نسخ منطق دردشة Ashare V2 إلى إيجار حرفيّاً — البنية متشابهة، لكن
  الكيانات تختلف.

## التشغيل في الإنتاج — Redis

افتراضيّاً in-memory cache + per-process tracker (instance واحدة). للنشر
متعدّد الـ instances اضبط:

```jsonc
"Cache":    { "Redis": { "ConnectionString": "redis-host:6379,password=...,abortConnect=false" } },
"Realtime": { "Redis": { "ConnectionString": "<same>" } }
```

عندها `ICache` ينقلب إلى Redis، `IConnectionTracker` إلى نسخة Redis، و
SignalR backplane يفعَّل. كلّ نسخ Ejar تتشارك حالة قنوات الدردشة والإشعارات.

> الإجابة على سؤال "هل أحتاج Redis منفصلاً عن قاعدة البيانات؟": نعم. Redis
> هو لـ التخزين المؤقّت ودردشة الزمن الحقيقيّ، قاعدة البيانات (SQL Server)
> هي للـ persistence الدائم.

## مراجع داخل الريبو

- `Apps/Ejar/Customer/Backend/Ejar.Api/Controllers/CatalogController.cs`
  — كل عمليّات النشر والمحادثة والشكاوى.
- `Apps/Ejar/Customer/Backend/Ejar.Api/Controllers/HomeController.cs`
  — الواجهة الرئيسيّة، البحث، الفلاتر، صفحة التفاصيل.
- `Apps/Ejar/Customer/Frontend/Ejar.Web/Resources/Strings*.resx` —
  الترجمات.
