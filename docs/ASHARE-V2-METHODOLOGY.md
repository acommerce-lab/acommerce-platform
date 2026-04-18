# Ashare.V2 — منهجيّة التطبيق (OAM applied)

هذه الوثيقة تُلخّص كيف طُبِّق **Operation-Accounting Model** على
`Apps/Ashare.V2` بعد عدّة جولات من المراجعة، وتُحدّد قواعد عمل واضحة لمن
يُضيف ميزة لاحقاً. اقرأ `docs/MODEL.md` أولاً؛ هذه الوثيقة امتداد له.

المرجعان في المستودع:
- `Apps/Order/Customer/Frontend/Order.Web` — أنقى مثال لواجهة.
- `Apps/Ashare/Customer/Backend/Ashare.Api` — أنقى مثال للخدمة الخلفيّة V1.

---

## 1. القاعدة الحاكمة

> **كلّ تغيير في الحالة = عمليّة (Operation). الواجهة لا تلمس `HttpClient`
> ولا تُعدّل `Store` مباشرة. الصفحات تُنشئ عمليّة فقط.**

من العمليّة نحصل على أحد مسارين:
- **HTTP-bound**: العمليّة تحمل `Tag("client_dispatch","true")` →
  `HttpDispatcher` يلتقطها ويُحوّلها إلى طلب عبر `V2Routes`.
- **Local-only**: العمليّة لا تحمل `client_dispatch` →
  `OperationInterpreterRegistry` يمرّرها إلى المفسّر المناسب
  (`UiInterpreter`, `AuthInterpreter`) الذي يُعدّل `AppStore` ويُطلق
  `Store.NotifyChanged()`.

---

## 2. متى Contract ومتى Interceptor؟

المبدأ الذي وُجّهنا إليه: **إن كان الاحتياج مهيكلاً نضع عقداً، وإن كان
مائعاً نضع معترضاً.**

| الاحتياج | شكله في الكود | مثال V2 |
|---|---|---|
| خدمة خارجيّة مُلزمة بشكل محدّد من المدخلات/المخرجات | `ProviderContract` يُحقَن عبر DI | `ITranslationProvider`, `IOperationDispatcher` (HTTP), `ITimezoneProvider` |
| قيد/سياسة عرضيّة تمسّ عمليّات متعدّدة | `IOperationInterceptor` على وسم مشترك | `OwnershipInterceptor` (`owner_policy`), `ListingQuotaInterceptor`, `OperationLogInterceptor` |
| قيد محلّي لعمليّة واحدة فقط | `.Analyze(new XAnalyzer(...))` على الـ `Entry` | `RequiredFieldAnalyzer("subject")` |
| قيد يحتاج إلى DI/DB | `.Validate(...)` أثناء بناء العمليّة | فحص الوحدات الشحنيّة داخل الطلب |

**لماذا HTTP عقد وليس خدمة عاديّة؟** لأنّ شكل الاحتياج مهيكل (Method +
Path + Body + Headers). عزلها في عقد يسمح بتبديل مصدر البيانات:
خدمة خلفيّة واحدة، عدّة خدمات مصغّرة، أو قاعدة محلّية — دون تعديل
الصفحات.

**لماذا الترجمات عقد؟** نفس المبرّر: المفاتيح والقيم مهيكلة. التطبيق
الحالي يستخدم `EmbeddedTranslationProvider`، ويمكن استبداله بـ
`ApiTranslationProvider` عبر DI دون لمس أيّ صفحة.

---

## 3. النمط العام لصفحة Blazor

```razor
@inject AppStore Store
@inject AppStateApplier Applier   @* العمليّات المحلّية *@
@inject ClientOpEngine Engine      @* العمليّات HTTP *@
@inject ApiReader Api              @* القراءات فقط (GET) *@

@code {
    // HTTP (mutation)
    await Engine.ExecuteAsync<Data>(V2Ops.CreateBooking(id, d, n, g));

    // Local (UI pref / favorite / recent search)
    await Applier.ApplyLocalAsync(V2Ops.SetTheme("dark"));
    await Applier.ApplyLocalAsync(V2Ops.ToggleFavorite(id));

    // Read
    var env = await Api.GetAsync<List<ListingDto>>("/home/explore");
}
```

**ممنوع** في صفحة Blazor:
- `IHttpClientFactory`, `HttpClient` مباشرة.
- `Store.FavoriteListingIds.Add(...)` أو أيّ تعديل مباشر.
- `Store.Ui.Theme = ...` بدون المرور عبر `Applier`.

---

## 4. تصنيف عمليّات V2 (الموجودة في `V2Ops.cs`)

### HTTP-bound (تحمل `client_dispatch`)

| العمليّة | الوجهة | ملاحظات |
|---|---|---|
| `NafathStart` | `POST /auth/nafath/start` | `RequiredField + MaxLength(10)` |
| `ToggleListing` | `PATCH /my-listings/{id}/toggle` | `must_own` معترضاً |
| `CreateBooking` | `POST /bookings` | `Range(nights, guests)` + `must_not_own` |
| `StartConversation` | `POST /conversations` | `RequiredField(text)` + `must_not_own` |
| `SendMessage` | `POST /conversations/{id}/messages` | `TimezoneInterceptor` قبل العرض |
| `ReadNotification` | `PATCH /notifications/{id}` | — |
| `ReadAllNotifications` | `PATCH /notifications/read-all` | — |
| `FileComplaint` | `POST /complaints` | `RequiredField + MaxLength` |
| `ReplyComplaint` | `POST /complaints/{id}/replies` | — |
| `UpdateProfile` | `PUT /profile` | `RequiredField(fullName, phone)` |

### Local-only (بلا `client_dispatch`)

| العمليّة | المفسّر | الأثر |
|---|---|---|
| `SetTheme` | `UiInterpreter` | `Store.Ui.Theme` + `data-theme` |
| `SetLanguage` | `UiInterpreter` | `Store.Ui.Lang` + `L.Reload` |
| `SetCity` | `UiInterpreter` | `Store.Ui.City` |
| `ToggleFavorite` | `UiInterpreter` | `Store.FavoriteListingIds` |
| `AddRecentSearch` | `UiInterpreter` | `Store.RecentSearches` |
| `SignOut` | `AuthInterpreter` | مسح الحالة |

القاعدة: **إن كانت نتيجة العمليّة تعيش في ذاكرة الجلسة فقط (تفضيلات،
قوائم محلّية، مفضّلة) فهي local. إن كانت تُغيّر حالة في قاعدة البيانات
على الخادم فهي HTTP.**

---

## 5. الخدمة الخلفيّة (V2.Api)

كلّ Controller ينفّذ نفس النمط:

```csharp
var op = Entry.Create("booking.create")
    .From($"User:{userId}", 1, ("role", "booker"))
    .To($"Listing:{listingId}", 1, ("role", "booked"))
    .Tag("listing_id", listingId)
    .Tag("owner_policy", "must_not_own")     // يستهلكه OwnershipInterceptor
    .Analyze(new RangeAnalyzer("nights", () => nights, 1, 365))
    .Execute(async ctx => await _repo.AddAsync(booking, ct))
    .Build();
var env = await _engine.ExecuteEnvelopeAsync(op, booking, ct);
return this.OkEnvelope(env);
```

**ممنوع** في Controller:
- `_repo.AddAsync(...)` مباشرة خارج `Execute(...)`.
- `return Ok(data)` في mutation — استخدم `OkEnvelope`.
- قراءة `userId` من الـ body — تأتي من `HttpContext.Items["user_id"]`
  الذي يُعبّئه `CurrentUserMiddleware`.

المعترضات المسجّلة:
- `OwnershipInterceptor` على وسم `owner_policy` (`must_own` /
  `must_not_own`).
- `ListingQuotaInterceptor` على وسم `quota_check`.
- `OperationLogInterceptor` (post-phase) يكتب سطر audit.

---

## 6. ما كان خطأً وكيف صُحِّح

| الخطأ السابق | التصحيح |
|---|---|
| صفحات تستدعي `HttpClient` مباشرة | `ClientOpEngine.ExecuteAsync(Op)` + `HttpDispatcher` كعقد |
| `Store.FavoriteListingIds.Add(id)` داخل صفحة | `Applier.ApplyLocalAsync(V2Ops.ToggleFavorite(id))` |
| `Store.Ui.Theme = "dark"` داخل `MainLayout` | `Applier.ApplyLocalAsync(V2Ops.SetTheme("dark"))` |
| ملفّ `Translations.cs` ثابت داخل الواجهة | `ITranslationProvider` قابل للاستبدال |
| `TimezoneService` كخدمة عاديّة | سيتحوّل إلى `ITimezoneProvider` (ProviderContract) |
| Controller ينادي `_repo` مباشرة | `Entry.Create().Execute(...).Build()` + `ExecuteEnvelopeAsync` |
| ملكيّة العرض تُفحص داخل Controller | `OwnershipInterceptor` يلتقط `owner_policy` |
| `return Ok(data)` على mutation | `this.OkEnvelope(env)` بعد المحرّك |

---

## 7. قائمة تحقّق (Checklist) لميزة جديدة

**واجهة:**
1. أضف مصنعاً في `V2Ops.cs` بـ `Entry.Create(...)`.
2. هل تحتاج خادماً؟ ضع `Tag("client_dispatch","true")` وسجّل المسار في
   `V2Routes.cs`. إن لم تحتج فاتركها محلّية وأضف فرعاً في
   `UiInterpreter`.
3. في الصفحة: `Applier.ApplyLocalAsync` أو `Engine.ExecuteAsync`.
   لا `HttpClient`. لا تعديل `Store` مباشرة.
4. أضف محلّلات `.Analyze(...)` لأيّ قيد محلّي.

**خدمة خلفيّة:**
1. Controller يبني `Entry.Create(op_type).From(...).To(...).Tag(...)
   .Execute(async ctx => ... await _repo ...).Build()`.
2. القيود العرضيّة → وسم + Interceptor.
3. القيود المحلّية → `.Analyze(...)`.
4. `_engine.ExecuteEnvelopeAsync(op, data, ct)` ثم
   `this.OkEnvelope(env)`.
5. `userId` من `HttpContext.Items["user_id"]` فقط.

**مراجعة ذاتيّة قبل الالتزام:**
- `grep -rn "HttpClient\|HttpClientFactory" Apps/Ashare.V2/Customer/Frontend` → يجب ألّا يخرج شيء من الصفحات.
- `grep -rn "_repo\." Apps/Ashare.V2/Customer/Backend --include="*Controller.cs"` → لا نداء مباشر خارج `Execute`.
- كلّ mutation يرجع `OperationEnvelope` وليس dto خام.

---

## 8. دراسة حالة: عرض زمن الرسائل (V1 vs V2)

**V1** (`libs/frontend/ACommerce.Templates.Shared/AcChatPage.razor:46`):
```razor
<small>@m.CreatedAt.ToLocalTime().ToString("HH:mm")</small>
```
- `.ToLocalTime()` على Blazor Server يقرأ توقيت **الخادم** لا المتصفّح → الزمن خاطئ لكلّ مستخدم خارج منطقة الخادم.
- منطق العرض مبعثر في كلّ صفحة تعرض وقتاً.

**V2 المرحلة الأولى** (ChatRoom.razor):
```razor
@inject ITimezoneProvider Tz
<small>@Tz.FormatTime(m.SentAt)</small>
```
- أصحّ: عقد (Provider) يقرأ offset المتصفّح عبر JS.
- لكن منطق التحويل مبعثر في كلّ صفحة عرض.

**V2 المرحلة الثانية (الحالية) — معترض قبل العرض**:
```csharp
// TimezoneLocalizer — يمشي بانعكاس على شجرة Data، يحوّل DateTime → Local.
public async Task LocalizeAsync<T>(OperationEnvelope<T> env, bool forced = false) { ... }
```
```csharp
// ApiReader — نقطة الدخول الوحيدة لكلّ GET.
var env = await Api.GetAsync<Payload>($"/conversations/{Id}", localizeTimes: true);
```
```razor
@* الصفحة لا تعرف التوقيت أبداً — حقل DateTime بالفعل محلّي. *@
<small>@m.SentAt.ToString("HH:mm")</small>
```

**الفرق الجوهريّ**: في V1 كلّ صفحة مسؤولة عن التحويل. في V2 (الحاليّ):
- `ITimezoneProvider` عقد خارجيّ (offset من المتصفّح).
- `TimezoneLocalizer` معترض يمرّر على envelope عند حدّ البيانات (`ApiReader`).
- الصفحة تعرض كحقل DateTime عاديّ. إضافة حقل جديد يُلتقط تلقائيّاً دون تعديل.
- `Tz.FormatRelative` يبقى مفيداً للصياغة النسبيّة ("الآن"، "10د") فقط،
  وهي idempotent (تعمل بغضّ النظر عن Kind).

**التفعيل**: إمّا بوسم من الخادم `envelope.Operation.Tags["localize_times"]="true"`،
أو بعلم صريح `GetAsync(..., localizeTimes: true)` من نقطة الاستدعاء.

---

## 9. المراجع السريعة

- **المنهجية**: `docs/MODEL.md`, `docs/ARCHITECTURE.md`, `docs/LIBRARY-ANATOMY.md`.
- **أمثلة واجهة**: `Apps/Order/Customer/Frontend/Order.Web/ClientOps.cs` +
  `Apps/Order/Customer/Frontend/Order.Web/Components/Pages/*.razor`.
- **أمثلة خدمة خلفيّة**: `Apps/Ashare/Customer/Backend/Ashare.Api/Controllers/*Controller.cs` (V1).
- **الملفّات المحوريّة في V2**:
  - `Operations/V2Ops.cs` — سجلّ العمليّات.
  - `Operations/V2Routes.cs` — خريطة HTTP للعمليّات الـ HTTP-bound.
  - `Interpreters/UiInterpreter.cs`, `Interpreters/AuthInterpreter.cs` — محلّيات.
  - `Store/AppStateApplier.cs` — `ApplyLocalAsync`.
  - `Store/ITranslationProvider.cs` — عقد الترجمات.
  - `Api/Interceptors/*.cs` — معترضات الخادم.
  - `Api/Middleware/CurrentUserMiddleware.cs` — مصدر هويّة المستخدم.
