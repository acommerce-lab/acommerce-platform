# Ashare V2 — منصّة العقارات والإيجار قصير المدى

> **اقرأ أوّلاً**: `docs/MODEL.md` (نموذج العمليّات OAM)، `docs/ARCHITECTURE.md`،
> `CLAUDE.md` (القوانين السبعة)، `docs/ASHARE-V2-METHODOLOGY.md`. هذه
> الوثيقة تشرح **هذه المنصّة الفرعيّة بعينها**، لا الإطار العام.

## ما هي

Ashare V2 إعادة بناء كامل لتطبيق "عشير" القديم على المنصّة الحديثة. الفكرة:
**سوق إعلانات عقارات** — مزوّد يضع إعلاناً (شقّة، فيلا، مكتب، مساحة عمل…)
ويختار وحدة الإيجار الزمنيّة (يوميّ/شهريّ/سنويّ)؛ والعميل يبحث، يفلتر،
يحجز، يتراسل، يقيّم بعد الإقامة، ويرفع شكوى إن لزم.

> **تنبيه**: "عشير القديم" في `CLAUDE.md` يشير لتطبيق خارجيّ في
> `https://github.com/acommerce-lab/ACommerce.Libraries`. أمّا هذه المنصّة
> (Ashare V2) فتطبيق جديد على هذا الريبو، يحاكي تجربة عشير القديم على
> أُسس OAM السليمة.

## التطبيقات الفرعيّة

| التطبيق | الجمهور | الهدف |
|---|---|---|
| `Customer/Frontend/Ashare.V2.Web` | المستأجر | تصفّح، بحث، فلاتر متقدّمة، حجز، دردشة، تقييم، شكاوى |
| `Provider/Frontend/Ashare.V2.Provider.Web` | صاحب العقار / المُعلِن | إدارة الإعلانات، الردّ على المحادثات، الفواتير، الاشتراك |
| `Admin/Frontend/Ashare.V2.Admin.Web` | إدارة المنصّة | اعتماد الإعلانات، إدارة المستخدمين، الشكاوى، التصنيفات |

كلّ Frontend يقابله Backend (`Customer/Backend/Ashare.V2.Api`،
`Provider/Backend/...`، `Admin/Backend/...`). المنفذ في
`appsettings.Development.json`.

## كيف يعمل (الدفق التشغيليّ)

```
المُعلِن                         الـ Backend                      المستأجر
───────                          ─────────                        ────────
  │ 1. يشترك في خطّة                                                │
  │ ─────────────────► [POST /me/subscription]                    │
  │ 2. يضيف إعلاناً                                                  │
  │ ─────────────────► [POST /my-listings]                        │
  │                       ListingQuotaInterceptor يفحص الحدّ        │
  │                       OwnershipInterceptor يضع PartyId         │
  │                       PersistenceInterceptor يحفظ              │
  │                                                                │ 3. يبحث/يفلتر
  │                                  ◄──────── [GET /listings?...]
  │                                                                │ 4. يفتح تفاصيل
  │                                  ◄──────── [GET /space/{id}]
  │                                                                │ 5. يبدأ محادثة
  │  ◄──── realtime push ─── ─── ── [POST /conversations/start]
  │                                                                │ 6. يحجز
  │                                  ◄──────── [POST /bookings]
  │ 7. يؤكّد ────────► [POST /bookings/{id}/confirm]                │
  │                                                                │ 8. يقيّم
  │                                  ◄──────── [POST /bookings/{id}/review]
```

## القوانين الخاصّة بهذه المنصّة

بالإضافة إلى **القوانين السبعة العامّة**:

### 1. التحقّق بالنفاذ (Nafath) إلزاميّ للمزوّدين

كلّ Provider يجب أن يتحقّق هويّته عبر النفاذ قبل نشر أوّل إعلان. مزوّد
`ACommerce.Authentication.TwoFactor.Providers.Nafath.*` يُحقَن في
`Program.cs` ويُستهلَك حصراً عبر تجريد `ITwoFactorProvider` — لا
استدعاءات HTTP مباشرة من Controller للنفاذ.

### 2. الإعلان مملوك دائماً

كلّ عمليّة كتابة على Listing تمرّ عبر `OwnershipInterceptor` الذي يقارن
`PartyId` المرسِل بـ `Listing.OwnerPartyId`. لا اختصار. لا «if user.IsAdmin
يجوز» — إن كان المسؤول يحتاج تجاوز الملكيّة فهذه عمليّة منفصلة لها
محلّلها (`AdminOverrideAnalyzer`).

### 3. المحتوى متعدّد اللغات في الـ API لا في الترجمة

الإعلانات تخزَّن بـ `TitleAr/TitleEn/DescriptionAr/DescriptionEn` (Law 6
من `CLAUDE.md` — نتكيّف مع شكل البيانات). الترجمة `L["..."]` للنصوص
الثابتة فقط — لا تُنشئ مفاتيح ترجمة لقيم API.

### 4. Realtime عبر التجريد فقط

الإشعارات اللحظيّة (محادثة جديدة، رسالة، حجز مؤكَّد، شكوى مُحدَّثة) تمرّ
عبر `ACommerce.Realtime.Operations` و `ACommerce.Realtime.Client.*`.
**ممنوع** `IHubContext`/`Microsoft.AspNetCore.SignalR` في الـ Controllers
أو الصفحات.

### 5. الفواتير والاشتراكات تستخدم تجريد المدفوعات

اشتراك المُعلِن يمرّ عبر `IPaymentGateway` — Noon هو المزوّد الإنتاجيّ
المختار (`Apps/Ashare.V2/Customer/Backend/.../appsettings.Production.json`)،
لكن أيّ Controller يستخدم التجريد فقط. لو احتجنا تبديل المزوّد لاحقاً =
سطر واحد في `Program.cs`.

### 6. التخزين الكائنيّ (Object Storage) للصور

صور الإعلانات تُرفع عبر `IFileStorage`. الإنتاج: Aliyun OSS
(`ACommerce.Files.Storage.AliyunOSS`). التطوير: Local
(`wwwroot/uploads`). التبديل = سطر في `Program.cs` + قسم في `appsettings`.

## كيف يجب أن يعمل (الحالة المرجوّة)

- **Customer** كامل ومُترجَم تقريباً (راجع جدول `docs/I18N.md`).
- **Provider** و **Admin** بحاجة لاستخراج `L.T("ع","en")` المتبقّية إلى
  مفاتيح في `Resources/Strings.resx` و `Strings.ar.resx`.
- كلّ تطبيق Frontend يستخدم مكتبة الدردشة المجرّدة `ACommerce.Chat.Client.*`
  (لا اتّصال مباشر بـ SignalR من الصفحات).
- كلّ Backend يستخدم `ACommerce.Realtime.Operations` لبثّ الأحداث، و
  `ACommerce.Notifications.*` لإيصال الإشعارات (داخل التطبيق + Firebase
  Push للمحمول).
- الكيانات تُسجَّل في `EntityDiscoveryRegistry` قبل تهيئة `DbContext`،
  والمعلومات تُحفظ في SQL Server (إنتاج) أو SQLite (تطوير) عبر
  `ACommerce.SharedKernel.Infrastructure.EFCores`.

## مخطّط الكيانات (مختصر)

```
Profile
  ├── PhoneNumber, Email, NafathVerifiedAt?
  └── Subscription >── Plan
                    └── Invoice (>=0)

Listing (مملوك لـ Profile)
  ├── TitleAr/En, DescriptionAr/En
  ├── Category, Amenities[], City, District, GeoPoint
  ├── Price, TimeUnit (Hourly|Daily|Monthly|Yearly)
  ├── Photos[] (IFileStorage)
  ├── Status (Draft|Active|Paused|Removed)
  └── Booking[]
        ├── StartDate, EndDate, GuestCount
        ├── Status (Pending|Confirmed|Cancelled|Completed)
        └── Review? (after Completed)

Conversation
  ├── ParticipantPartyIds[]
  ├── ListingId? (إذا بُدئت من صفحة إعلان)
  └── Message[] (Sender, Body, SentAt, ReadBy[])

Notification (per User)
  ├── Type (NewMessage|BookingConfirmed|ReviewReceived|...)
  ├── ConversationId? / BookingId? / ListingId?
  └── ReadAt?

Complaint
  ├── Subject, Body, Status (Open|InReview|Resolved|Closed)
  └── Reply[]
```

## التشغيل في الإنتاج — Redis

ثلاث خدمات (Customer + Admin؛ Provider يستعمل Customer)؛ كلّها تبدأ بـ
in-memory cache افتراضيّاً. للإنتاج متعدّد الـ instances:

```jsonc
"Cache":    { "Redis": { "ConnectionString": "redis-host:6379,password=...,abortConnect=false" } },
"Realtime": { "Redis": { "ConnectionString": "<same>" } }
```

شرط: Customer و Admin backends يجب أن يشيرا لنفس Redis لتبادل قنوات الدردشة
بين الـ hubs (Customer Hub `/hubs/ashare-v2` و Admin Hub `/hubs/ashare-v2-admin`
مفصولان منطقيّاً، لكنّ مكتب الإشعارات والقنوات يعتمد على نفس قاموس الاتّصالات).

> SignalR Redis Backplane هي الآليّة الرسميّة لتبادل الـ groups بين instances؛
> `RedisConnectionTracker` هي آليّتنا لـ userId→connectionId بين الـ hubs.

## مراجع داخل الريبو

- `Apps/Ashare.V2/Customer/Backend/Ashare.V2.Api/Program.cs` — قالب Backend
  ملتزم بكل المزوّدات الإنتاجيّة (Nafath/Noon/Aliyun/Firebase/SMTP).
- `docs/ASHARE-V2-METHODOLOGY.md` — تفاصيل المنهجيّة.
- `docs/PRODUCTION-INTEGRATION.md` — كيف نُحقَن المزوّدات الإنتاجيّة.
