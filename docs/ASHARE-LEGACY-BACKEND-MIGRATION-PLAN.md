# خطّة ترحيل الخدمة الخلفيّة لعشير القديمة إلى V2

**المصدر**: `/tmp/ACommerce.Libraries/Apps/Ashare.Api/` (ASP.NET Core 8 + EF Core + SignalR + Firebase + Noon + GCS/OSS).
**الوجهة**: `Apps/Ashare.V2.Api` — خدمة OAM (Operation-Accounting Model) خالصة.
**المراجع الداخليّة**:
- جرد المصدر: `docs/ASHARE-LEGACY-INVENTORY.md`
- الاستشهادات المنهجيّة: `docs/ASHARE-METHODOLOGY-CITATIONS.md`
- المرجع التطبيقيّ (لا مصدر): `Apps/Order.Api`, `Apps/Vendor.Api`, `Apps/Ashare.Api` في المستودع الحاليّ.

**نطاق الخطّة**: 10 متحكّمات محلّية + 23 متحكّماً من مكتبة ACommerce + 2 SignalR hubs + الخدمات + المخاوف العرضيّة (مصادقة/دفع/تخزين/إشعارات/بذور).

---

## 0. المبادئ الحاكمة

كلّ مرحلة ملزَمة بـ:

1. **القانون 1 (CLAUDE.md:41-53)**: لا `_repo.AddAsync(entity)` من متحكّم — كلّ طفرة حالة:
   ```csharp
   var op = Entry.Create("thing.create")
       .From($"User:{ownerId}", 1, ("role","owner"))
       .To($"Thing:{id}",       1, ("role","created"))
       .Tag("name", name)
       .Analyze(new RequiredFieldAnalyzer("name", () => name))
       .Execute(async ctx => await _repo.AddAsync(entity, ctx.CancellationToken))
       .Build();
   var envelope = await _engine.ExecuteEnvelopeAsync(op, entity, ct);
   ```

2. **القانون 2 (CLAUDE.md:56-58)**: كلّ استجابة endpoint = `OperationEnvelope<T>`. حتى القراءات: `return this.OkEnvelope("thing.list", data);`.

3. **القانون 3 (CLAUDE.md:60-64)**: `ListAllAsync(ct)` يأخذ `CancellationToken`؛ `GetAllWithPredicateAsync(predicate, bool includeDeleted)` لا يأخذه.

4. **الـ Entry الأدنى (MODEL.md:65-82)**: `From + To + BalanceAnalyzer` إلزاميّ؛ `AccountingBuilder` يفرض الوسم `pattern: accounting`.

5. **Analyzer vs Interceptor vs ProviderContract (MODEL.md:83-142)**:
   - **Analyzer**: قيد محلّيّ لعمليّة واحدة (`.Analyze()`).
   - **Interceptor**: عابر، مسجَّل في DI، يُطبَّق بمطابقة الوسوم تلقائيّاً.
   - **ProviderContract**: اعتماد خارجيّ إلزاميّ (مصادقة/دفع/تخزين/FCM) — واجهة مُلزمة بـ `.Requires<T>()`.
   - **Sealed/ExcludeInterceptor**: لحجب المعترضات عن عمليّات حسّاسة.

6. **LIBRARY-ANATOMY ثلاثيّة الطبقات**:
   - الطبقة 1 — محاسبة نقيّة (Entry types + Analyzers + Tags).
   - الطبقة 2 — Provider contracts (المكتبة تشحن بصفر تنفيذ).
   - الطبقة 3 — Interceptors قابلة للحقن (Quota, Audit, Cache, Translation، ... .

7. **القانون 6 — Adapt to real data (CLAUDE.md:76-85)**: بيانات الإنتاج تُستوعَب بشكلها، مفاتيح غير معروفة تُحفظ في `DynamicAttribute`.

**طبقات التحقّق الخلفيّة (بعد كلّ مرحلة)**:
- `dotnet build` بدون تحذيرات.
- `dotnet test tests/Ashare.V2.Api.Tests` لكلّ وحدات الاختبار المضافة.
- اختبار تكامليّ: كلّ endpoint يُعيد `OperationEnvelope<T>` صالحاً.
- grep على مصدر المتحكّمات: لا `_repo.AddAsync`/`UpdateAsync`/`DeleteAsync` مباشرة (يجب أن تكون داخل `.Execute(ctx => ...)`).
- grep: لا `return Ok(data)` خام خارج `OkEnvelope`.

---

## 1. المراحل

### المرحلة 0 — التأسيس (Bootstrap)

**النطاق**:
- إنشاء `Apps/Ashare.V2.Api` (ASP.NET Core — .NET 10).
- `ApplicationDbContext` بـ EF Core + الاتصال (PostgreSQL/SQL Server بحسب بيئة الإنتاج القديمة).
- `DataProtectionKeyContext` لحفظ مفاتيح Data Protection (مطابق للقديم).
- تسجيل الكيانات في `EntityDiscoveryRegistry` (BUILDING-A-BACKEND.md:112-115):
  ```csharp
  EntityDiscoveryRegistry.RegisterEntity(typeof(Profile));
  EntityDiscoveryRegistry.RegisterEntity(typeof(TwoFactorChallengeRecord));
  EntityDiscoveryRegistry.RegisterEntity(typeof(DeviceTokenEntity));
  EntityDiscoveryRegistry.RegisterEntity(typeof(ProductCategory));
  EntityDiscoveryRegistry.RegisterEntity(typeof(AttributeDefinition));
  EntityDiscoveryRegistry.RegisterEntity(typeof(CategoryAttributeMapping));
  EntityDiscoveryRegistry.RegisterEntity(typeof(Product));
  EntityDiscoveryRegistry.RegisterEntity(typeof(ProductListing));
  EntityDiscoveryRegistry.RegisterEntity(typeof(Order));
  EntityDiscoveryRegistry.RegisterEntity(typeof(Booking));
  EntityDiscoveryRegistry.RegisterEntity(typeof(Payment));
  EntityDiscoveryRegistry.RegisterEntity(typeof(Subscription));
  EntityDiscoveryRegistry.RegisterEntity(typeof(Chat));
  EntityDiscoveryRegistry.RegisterEntity(typeof(ChatMessage));
  EntityDiscoveryRegistry.RegisterEntity(typeof(Notification));
  EntityDiscoveryRegistry.RegisterEntity(typeof(Complaint));
  EntityDiscoveryRegistry.RegisterEntity(typeof(ComplaintReply));
  EntityDiscoveryRegistry.RegisterEntity(typeof(LegalPage));
  EntityDiscoveryRegistry.RegisterEntity(typeof(AttributionSession));
  // ...
  ```
- تسجيل `OpEngine` (BUILDING-A-BACKEND.md:135-136):
  ```csharp
  builder.Services.AddScoped<OpEngine>(sp =>
      new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));
  ```
- `AddCultureStack()` + middleware `UseCultureContext()` بين Routing والـ controllers (CULTURE.md:42-49).
- `UseAuthentication()` + `UseAuthorization()` بـ JWT Bearer (يُضبط في المرحلة 1).
- Serilog + Swagger + Health checks — مطابقة للقديم.
- Middleware `GlobalExceptionMiddleware` يحوّل الاستثناءات إلى `OperationEnvelope<object>` مع `success: false`.

**معايير القبول**:
- `dotnet build` نظيف.
- `GET /health` → 200 + envelope.
- `GET /swagger` يعمل ويَعرض العمليّات الموجودة.
- grep على `Program.cs`: يحوي `EntityDiscoveryRegistry.RegisterEntity` لكلّ كيان مخطَّط.

**Definition of Done**: خدمة تقلع، health check أخضر، migration تُنفَّذ بدون خطأ، Culture context يُحمَّل.

**Commit**: `feat(ashare-v2-api): bootstrap — entities, OpEngine, culture stack, Serilog`.

### المرحلة 1 — المصادقة (Nafath 2FA + JWT + Admin login)

**المصدر**: `AuthController` (6 endpoints) + `NafathWebhookController` (1 endpoint) في الجرد القديم.

**Provider contracts** (إلزاميّة — المكتبة بلا تنفيذ):
- `ITwoFactorAuthenticationProvider` — تنفيذ `NafathTwoFactorProvider` في shell الإنتاج.
- `ITwoFactorSessionStore` — ذاكرة مؤقّتة للجلسات المعلَّقة (Redis أو EF).
- `IAuthenticationProvider` — إصدار JWT + Admin password verification.
- `IMarketingEventTracker` — يُحقَن اختياريّاً لتسجيل `registration`/`login`.

**العمليّات**:
| القديم | الجديد | Entry shape |
|---|---|---|
| `POST /api/auth/nafath/initiate` | `auth.nafath.initiate` | `From(Anon)` → `To(Nafath:{transId})` + `Tag("nationalId", ...)` + Analyzer `NationalIdAnalyzer` |
| `GET /api/auth/nafath/status` | `auth.nafath.status` | قراءة — `OkEnvelope("auth.nafath.status", state)` |
| `POST /api/auth/nafath/complete` | `auth.nafath.complete` | `From(Nafath:{transId})` → `To(Profile:{id})` + `Execute` يُنشِئ/يُحدِّث البروفايل + يُصدر JWT |
| `POST /api/auth/admin/login` | `auth.admin.login` | `From(Admin:{username})` → `To(Session:{token})` + `Tag("sealed","true")` (يحجب معترضات) |
| `GET /api/auth/me` | `auth.me` | قراءة — من `HttpContext.User` + repo `Profile` |
| `POST /api/auth/logout` | `auth.logout` | `From(Session:{id})` → `To(Void)` + مسح الجلسة |
| `POST /api/auth/nafath/webhook` | `auth.nafath.webhook` | يبقى endpoint رفيع — يُحوِّل Payload إلى استدعاء `auth.nafath.webhook.process` داخليّ بعد التحقّق من التوقيع |

**Interceptors مُقترَحة**:
- `MarketingEventInterceptor` على `Tag("marketing","registration"/"login")` → يستدعي `IMarketingEventTracker`.
- `AuditInterceptor` على كلّ عمليّات `auth.*` ما لم تحمل `sealed`.

**Analyzers محليّة**:
- `NationalIdAnalyzer` — يتحقّق من الصحّة الهيكليّة لرقم الهويّة السعوديّ.
- `WebhookSignatureAnalyzer` — يتحقّق من توقيع webhook نفاذ.

**معايير القبول**:
- تكامل: دورة initiate → (محاكاة webhook) → complete → JWT صالح → `/api/auth/me` يُعيد الملفّ.
- `auth.admin.login` يرفض كلمة سرّ خاطئة عبر `PredicateAnalyzer`.
- grep على `AuthController`: كلّ الـ endpoints تعيد `OperationEnvelope`.
- لا استدعاء مباشر لـ Nafath من المتحكّم — كلّ الاتّصال عبر `ITwoFactorAuthenticationProvider`.

**Definition of Done**: مسار مصادقة نفاذ يعمل end-to-end، Admin login يُصدر JWT صالح.

**Commit**: `feat(ashare-v2-api): auth — nafath 2FA + admin login + jwt via provider contracts`.

### المرحلة 2 — البروفايلات والمستخدمون

**المصدر**: `ProfilesController` (من مكتبة ACommerce، مستخدَم في الجرد).

**العمليّات**:
| القديم | الجديد | Entry shape |
|---|---|---|
| `GET /api/profiles/me` | `profiles.me` | قراءة (قد يُعاد استخدامها من `auth.me`) |
| `GET /api/profiles/{id}` | `profiles.get` | قراءة |
| `PUT /api/profiles/me` | `profiles.update` | `From(User:{id})` → `To(Profile:{id})` + `Tag("role","self-update")` |
| `POST /api/profiles/me/avatar` | `profiles.avatar.update` | `From(User)` → `To(Profile)` + `Tag("field","avatarUrl")` + `Execute` يضع `AvatarUrl` |
| `GET /api/profiles/{id}/listings` | `profiles.listings.list` | قراءة مع فلتر |

**Analyzers**:
- `ProfileUpdateAnalyzer` — يمنع تعديل حقول محميّة (e.g. `NationalId`, `Role`) إلاّ من Admin.
- `AvatarUrlAnalyzer` — يتحقّق أنّ الـ URL ضمن نطاق `IStorageProvider`.

**Interceptors**:
- `AuditInterceptor` على كلّ `profiles.update.*`.
- `ProfileCompletionInterceptor` (اختياريّ) — يُحسب نسبة اكتمال البروفايل ويُوسم على الردّ.

**معايير القبول**:
- مستخدم يُحدِّث بياناته ويرى التغيير في `GET /api/profiles/me`.
- محاولة تحديث `Role` من مستخدم عاديّ → رفض مع envelope `success:false`.
- Audit entries تظهر في `AuditLog` (المرحلة 13).

**Definition of Done**: CRUD على البروفايلات عبر OAM، مع منع تعديل الحقول الحسّاسة.

**Commit**: `feat(ashare-v2-api): profiles CRUD via OAM + protected fields analyzer`.

### المرحلة 3 — الكتالوج (الفئات + تعريفات السمات + Mappings)

**المصدر**: `CategoryAttributesController` (4 endpoints) + `AttributeDefinitionsController` + `UnitsController` + `CurrenciesController` + `ContactPointsController` + `LocationSearchController`.

**الكيانات**:
- `ProductCategory` — الفئات الخمس: سكني، طلب سكن، طلب شريك، إداريّ، تجاريّ (من `AshareSeedDataService`).
- `AttributeDefinition` — تعريفات السمات (Title, Price, Duration, TimeUnit, Location, Images, PropertyType, Furnished, Amenities, ...).
- `CategoryAttributeMapping` — ربط الفئة بسماتها المطلوبة/الاختياريّة.
- `Unit`, `Currency`, `ContactPoint`.

**العمليّات**:
| القديم | الجديد |
|---|---|
| `GET /api/categoryattributes/mappings` | `categories.mappings.list` |
| `GET /api/categoryattributes/category/{categoryId}` | `categories.attributes.get` |
| `GET /api/categoryattributes/categories` | `categories.list` |
| `GET /api/attributes` | `attributes.list` |
| `POST /api/attributes` (admin) | `attributes.create` — Entry من Admin إلى AttributeDefinition |
| `PUT /api/attributes/{id}` | `attributes.update` |
| `GET /api/units` / `currencies` / `contact-points` | `units.list`, `currencies.list`, `contact-points.list` |
| `GET /api/locations/search?q=` | `locations.search` |

**Template السمات** (DYNAMIC-ATTRIBUTES.md:13-20):
- `Category.AttributeTemplateJson` هو الـ schema الذي يُبنى منه form الإعلان في الواجهة.
- السمات غير المعروفة التي يدخلها المستخدم تُحفظ خام في `Listing.DynamicAttributesJson` (Snapshot — DYNAMIC-ATTRIBUTES.md:47-54).

**معايير القبول**:
- `GET /api/categoryattributes/category/{id}` يُعيد كلّ السمات + قيمها مع `colorHex`, `imageUrl` إن وُجدت.
- اختبار: فئة "طلب شريك" تعيد سمات `PersonalName, Age, Gender, Nationality, Job, MinPrice, MaxPrice, Smoking` مع القيم.
- envelope يحوي `Template` جاهزاً لاستهلاك الواجهة.

**Definition of Done**: كامل الكتالوج يعمل، الواجهة تستطيع بناء forms ديناميكيّة.

**Commit**: `feat(ashare-v2-api): catalog — categories, attributes, units, currencies, locations`.

### المرحلة 4 — المنتجات والعروض (Products + ProductListings)

**المصدر**: `ProductsController` + `ProductListingsController` + `AdminListingsController`.

**الكيانات**:
- `Product` — مساحة قابلة للنشر (أصل).
- `ProductListing` — عرض نشط من مساحة، يحمل `DynamicAttributesJson` و `ImageUrls` و `Status`.

**العمليّات الأساسيّة**:
| القديم | الجديد | Entry shape |
|---|---|---|
| `GET /api/listings/featured` | `listings.featured` | قراءة مع `Tag("cacheable","true")` |
| `GET /api/listings/new` | `listings.new` | قراءة + Tag cache |
| `GET /api/listings` | `listings.list` | قراءة مع فلاتر |
| `POST /api/listings/search` | `listings.search` | قراءة مع body filters |
| `GET /api/listings/{id}` | `listings.get` | قراءة — مع تضمين Product + Category + DynamicAttrs |
| `POST /api/listings` | `listings.create` | `From(User:{hostId})` → `To(Listing:{id})` + أكبر عدد من Analyzers (انظر ملحق د wizard) |
| `PUT /api/listings/{id}` | `listings.update` | `From(User)` → `To(Listing)` + `Tag("role","host")` |
| `DELETE /api/listings/{id}` | `listings.delete` | soft-delete عبر `IncludeDeleted=true` |
| `POST /api/admin/listings/{id}/moderate` | `admin.listings.moderate` | `From(Admin)` → `To(Listing)` + `Tag("decision","approve/reject")` |

**Analyzers على `listings.create`**:
- `RequiredFieldAnalyzer("title"|"price"|"categoryId")`.
- `PredicateAnalyzer("images_min_1", ctx => urls.Count >= 1)`.
- `PredicateAnalyzer("category_exists", ctx => _categoryRepo.Exists(categoryId))`.
- `DynamicAttributesAnalyzer` — يتحقّق أنّ السمات المطلوبة في Template موجودة؛ السمات الزائدة تُحفظ بدون منع (قانون 6).

**Interceptors**:
- `CacheInterceptor` على `Tag("cacheable","true")` — يستبدل `CacheWarmupService` القديم.
- `ContentFilterInterceptor` (LIBRARY-ANATOMY.md:148-155) — يفحص النصوص للكلمات الممنوعة قبل النشر.
- `ListingQuotaInterceptor` — كلّ مستضيف له حدّ أقصى من الإعلانات النشطة حسب اشتراكه (المرحلة 7).

**معايير القبول**:
- مستخدم مُسجَّل ينشئ listing جديد، يظهر في `listings.new` + `listings.list`.
- DynamicAttributes تُحفَظ كاملةً بما فيها الحقول غير المعرّفة في Template.
- Admin يرفض listing → الحالة تتغيّر + ContentFilter يعمل على النصوص.

**Definition of Done**: دورة إعلان كاملة (إنشاء/تحديث/حذف/moderation) عبر OAM مع cache + quota.

**Commit**: `feat(ashare-v2-api): listings CRUD + moderation + dynamic attributes + quota interceptor`.

### المرحلة 5 — الحجوزات (Bookings)

**المصدر**: `BookingsController` (من مكتبة ACommerce).

**العمليّات**:
| القديم | الجديد | Entry shape |
|---|---|---|
| `GET /api/bookings` (للمستخدم) | `bookings.list.mine` | قراءة مع فلتر `status` |
| `GET /api/bookings/{id}` | `bookings.get` | قراءة + تحقّق صلاحيّة (الزبون أو المستضيف) |
| `POST /api/bookings/quote` | `bookings.quote` | قراءة تسعيريّة قبل الإنشاء (دون Entry مُحاسَبيّ إلزاميّ — قد تكون معلوماتيّة) |
| `POST /api/bookings` | `bookings.create` | `From(User:{customerId})` → `To(Listing:{id})` + `Tag("start","end","price")` + عدّة Analyzers |
| `POST /api/bookings/{id}/confirm` | `bookings.confirm` | `From(Host)` → `To(Booking)` + `Tag("status","confirmed")` |
| `POST /api/bookings/{id}/reject` | `bookings.reject` | `From(Host)` → `To(Booking)` + `Tag("reason", text)` |
| `POST /api/bookings/{id}/cancel` | `bookings.cancel` | `From(User)` → `To(Booking)` + policy analyzer |

**Analyzers على `bookings.create`**:
- `BookingDateRangeAnalyzer` — start < end، start >= now، end <= now+1yr.
- `BookingOverlapAnalyzer` — لا تعارض مع حجز مؤكَّد آخر على نفس Listing.
- `ListingAvailabilityAnalyzer` — Listing في الحالة `Active`.
- `PricingAnalyzer` — السعر يتطابق مع `quote` السابق (منع التلاعب).

**Interceptors**:
- `NotifyOnBookingInterceptor` على `Tag("notify","booking")` → يرسل إشعار SignalR + Firebase (المرحلة 9).
- `AuditInterceptor`.

**Sub-operations** (MODEL.md:65-82 — `SubEntries` في Entry):
- `bookings.confirm` يُطلق sub-entry `notifications.dispatch.booking.confirmed` على الزبون.
- `bookings.reject` يُطلق sub-entry مع سبب الرفض.

**معايير القبول**:
- تكامل: إنشاء حجز → تأكيد → التأكّد من وصول إشعار للزبون.
- رفض تداخل حجز مع حجز مؤكَّد — envelope `success:false` مع سبب واضح.
- سياسة الإلغاء: إلغاء قبل 24 ساعة = مجاناً، بعدها = رسوم (عبر PredicateAnalyzer).

**Definition of Done**: دورة حجز كاملة عبر OAM مع تأكيد/رفض/إلغاء + إشعارات.

**Commit**: `feat(ashare-v2-api): bookings lifecycle via OAM with overlap + availability analyzers`.

### المرحلة 6 — المدفوعات (Noon Gateway)

**المصدر**: `PaymentsController` + `PaymentCallbackController` (2 endpoints GET/POST).

**Provider contract** (إلزاميّ):
```csharp
public interface IPaymentGateway
{
    Task<PaymentInitResult> InitiateAsync(PaymentInitRequest req, CancellationToken ct);
    Task<PaymentVerificationResult> VerifyAsync(string gatewayRef, CancellationToken ct);
    Task<PaymentCaptureResult> CaptureAsync(string gatewayRef, decimal amount, CancellationToken ct);
    Task<PaymentRefundResult> RefundAsync(string gatewayRef, decimal amount, string reason, CancellationToken ct);
    string Name { get; }     // "noon", "stripe", ...
    string VerifyWebhookSignature(HttpRequest request);
}
```
تنفيذ `NoonPaymentGateway` في مشروع منفصل `Ashare.Payments.Noon` يُحقَن من `Program.cs`.

**العمليّات**:
| القديم | الجديد | Entry shape |
|---|---|---|
| `POST /api/payments/initiate` | `payments.initiate` | `From(Booking/Subscription)` → `To(Payment:{id})` + `Tag("gateway","noon")` — يُصدر paymentUrl |
| `GET /api/payments/{id}` | `payments.get` | قراءة |
| `POST /host/payment/callback` (Noon) | `payments.callback.process` | Webhook — يُحوَّل إلى عمليّة داخليّة بعد التحقّق من التوقيع |
| `POST /api/payments/{id}/refund` (admin) | `payments.refund` | `From(Admin)` → `To(Payment)` + `Tag("amount","reason")` |

**Analyzers/Interceptors**:
- `PaymentCallbackSignatureAnalyzer` — تحقّق من توقيع Noon webhook.
- `PaymentStatusInterceptor` على `Tag("notify","payment")` → يُرسِل SignalR notification لمجموعة `payment_{orderId}`.
- `CaptureOnSuccessInterceptor` — عندما `status == captured` أو `resultCode == 0`، يُطلَق sub-entry `bookings.confirm` أو `subscriptions.activate`.

**Sub-operations**:
- نجاح دفع حجز → `bookings.confirm`.
- نجاح دفع اشتراك → `subscriptions.activate` + تجديد الحدّ الأقصى لإعلانات المستضيف.

**معايير القبول**:
- تكامل: initiate → محاكاة webhook Noon ناجح → الحجز يصبح مؤكَّداً + notification يصل للزبون.
- Webhook بتوقيع غير صالح → 401 + envelope خطأ + log.
- Refund يُعدّل الحالة ويُرسل إشعاراً.

**Definition of Done**: دورة دفع كاملة مع Noon + webhook موثَّق + refund.

**Commit**: `feat(ashare-v2-api): payments via Noon provider + callback webhook + sub-entries`.

### المرحلة 7 — الاشتراكات (Subscriptions)

**المصدر**: `SubscriptionsController`.

**الكيانات**:
- `SubscriptionPlan` — الخطط (Basic, Pro, Premium بحسب الإنتاج).
- `Subscription` — اشتراك مستخدم فعليّ + تواريخ + حدود.

**العمليّات**:
| القديم | الجديد |
|---|---|
| `GET /api/subscriptions/plans` | `subscriptions.plans.list` |
| `POST /api/subscriptions/checkout` | `subscriptions.checkout` — يُنشِئ `Payment` ويُرجع `paymentUrl` |
| `GET /api/subscriptions/current` | `subscriptions.current` |
| `POST /api/subscriptions/{id}/cancel` | `subscriptions.cancel` |
| `POST /api/subscriptions/{id}/renew` | `subscriptions.renew` (internal — يُطلق من `payments.callback.process`) |

**Entry for `subscriptions.checkout`**:
```csharp
Entry.Create("subscription.checkout")
    .From($"User:{userId}", 1, ("role","subscriber"))
    .To($"SubscriptionPlan:{planSlug}", 1, ("role","target"))
    .Tag("plan", planSlug).Tag("billing-period", period)
    .Analyze(new ActiveSubscriptionAnalyzer(userId))   // يمنع اشتراكاً مكرَّراً
    .Analyze(new PlanExistsAnalyzer(planSlug))
    .Build();
```

**Interceptors**:
- `QuotaInterceptor` (LIBRARY-ANATOMY.md:148-155) على `Tag("quota_check","true")` — يُسجَّل في `listings.create` ليفحص حدّ الإعلانات بناءً على خطّة المستضيف.
- `RenewalReminderInterceptor` — عمليّة مجدولة (خارج النطاق الأوّليّ — تُؤجَّل).

**معايير القبول**:
- مستخدم يشترك في "Basic" → يدفع → الاشتراك يُفعَّل → limit الإعلانات يُطبَّق.
- يحاول إنشاء إعلان سادس رغم حدّ 5 → envelope `success:false` + رسالة واضحة.

**Definition of Done**: الاشتراكات تؤثّر على صلاحيّات المستضيف (quotas) عبر Interceptors.

**Commit**: `feat(ashare-v2-api): subscriptions checkout + quota interceptor on listing creation`.

### المرحلة 8 — المحادثات والـ Realtime (ChatHub + ChatsController)

**المصدر**: `ChatsController` + `ChatHub` (/hubs/chat).

**Provider contract**:
```csharp
public interface IRealtimeMessageBus
{
    Task PublishToUserAsync(Guid userId, string topic, object payload, CancellationToken ct);
    Task PublishToGroupAsync(string group, string topic, object payload, CancellationToken ct);
}
```
تنفيذ `SignalRMessageBus` (افتراضيّ) + إمكانيّة استبداله بـ Redis streams لاحقاً بدون لمس المتحكّم.

**العمليّات**:
| القديم | الجديد |
|---|---|
| `GET /api/chats` | `chats.list.mine` |
| `GET /api/chats/{id}` | `chats.get` |
| `POST /api/chats/start` | `chats.start` — `From(User)` → `To(Chat:{new})` + `Tag("context","listing:{id}")` |
| `GET /api/chats/{id}/messages` | `chats.messages.list` |
| `POST /api/chats/{id}/messages` | `chats.message.send` — Entry + sub-entry ينشر عبر `IRealtimeMessageBus` |
| `POST /api/chats/{id}/read` | `chats.read` |

**Entry shape for `chats.message.send`**:
```csharp
Entry.Create("chat.message.send")
    .From($"User:{senderId}", 1, ("role","sender"))
    .To($"Chat:{chatId}",     1, ("role","channel"))
    .Tag("notify","chat").Tag("audit","true")
    .Analyze(new ChatMembershipAnalyzer(senderId, chatId))
    .Analyze(new MessageLengthAnalyzer(max: 4000))
    .Execute(async ctx => {
        var msg = new ChatMessage { ... };
        await _chatRepo.AddAsync(msg, ctx.CancellationToken);
        ctx.Set("messageId", msg.Id);
    })
    .Build();
```

**Interceptors**:
- `ChatNotifyInterceptor` على `Tag("notify","chat")` — يستدعي `IRealtimeMessageBus.PublishToUserAsync(receiverId, "chat:new", msg)` + يُطلق إشعار Firebase إن كان المستقبِل offline.
- `ChatMembershipInterceptor` (قد يُستبدَل بـ Analyzer محليّ).

**SignalR Hub**:
- `/hubs/chat` يبقى endpoint الـ hub، لكن منطقه يقتصر على:
  - Authentication check + join/leave group.
  - استدعاء عمليّات OAM بدل الإرسال المباشر.
- Hub نفسه **لا يحتوي أيّ طفرة حالة مباشرة** — كلّ شيء عبر `OpEngine`.

**معايير القبول**:
- مستخدمان يتبادلان رسائل عبر SignalR بزمن استجابة ≤ 500ms.
- إعادة اتصال تلقائيّة بعد قطع شبكة.
- لا `_chatRepo.AddAsync` خارج `.Execute(ctx => ...)`.

**Definition of Done**: محادثات فوريّة تعمل مع OAM خالصة.

**Commit**: `feat(ashare-v2-api): chats + signalR hub as realtime provider contract`.

### المرحلة 9 — الإشعارات (Firebase FCM + NotificationHub)

**المصدر**: `NotificationsController` (6) + `AdminNotificationsController` (7) + `NotificationHub` (/hubs/notifications) + `AshareNotificationService`.

**Provider contracts**:
```csharp
public interface IPushNotificationProvider  // Firebase impl
{
    Task<PushResult> SendToDeviceAsync(string deviceToken, PushMessage msg, CancellationToken ct);
    Task<PushResult> SendToTopicAsync(string topic, PushMessage msg, CancellationToken ct);
    Task<int> SendBatchAsync(IEnumerable<string> tokens, PushMessage msg, CancellationToken ct);
}

public interface IFirebaseTokenStore
{
    Task RegisterTokenAsync(Guid userId, string token, DeviceInfo info, CancellationToken ct);
    Task RevokeTokenAsync(Guid userId, string token, CancellationToken ct);
    Task<IReadOnlyList<string>> GetActiveTokensAsync(Guid userId, CancellationToken ct);
}
```

**العمليّات (User)**:
| القديم | الجديد |
|---|---|
| `POST /api/notifications/device-token` | `notifications.device.register` |
| `DELETE /api/notifications/device-token` | `notifications.device.revoke` |
| `GET /api/notifications/devices/count` | `notifications.devices.count` |
| `POST /api/notifications/test` | `notifications.test` (dev-only) |
| `GET /api/notifications/settings` | `notifications.settings.get` |
| `PUT /api/notifications/settings` | `notifications.settings.update` |
| `GET /api/notifications` | `notifications.list.mine` |
| `POST /api/notifications/{id}/read` | `notifications.mark.read` |

**العمليّات (Admin)**:
| القديم | الجديد |
|---|---|
| `GET /api/admin/notifications/users` | `admin.notifications.users.list` |
| `POST /api/admin/notifications/send` | `admin.notifications.send` — `From(Admin)` → `To(Users[])` |
| `POST /api/admin/notifications/broadcast` | `admin.notifications.broadcast` |
| `GET /api/admin/notifications/stats` | `admin.notifications.stats` |

**Entry shape for `admin.notifications.send`**:
```csharp
Entry.Create("admin.notifications.send")
    .From($"Admin:{adminId}", 1, ("role","sender"))
    .To($"NotificationBatch:{batchId}", targetUserIds.Count, ("role","recipients"))
    .Tag("type", type).Tag("priority", priority).Tag("audit","true")
    .Analyze(new AdminOnlyAnalyzer())
    .Analyze(new NotificationContentAnalyzer())
    .Execute(async ctx => {
        foreach (var uid in targetUserIds) { await _repo.AddAsync(new Notification { UserId = uid, ... }); }
        await _bus.PublishToUsersAsync(targetUserIds, "notif:new", payload);
        var tokens = await _tokenStore.GetActiveTokensAsync(targetUserIds);
        await _push.SendBatchAsync(tokens, msg);
    })
    .Build();
```

**AshareNotificationService** القديمة تختفي — كلّ دوالّها (SendNewMessageNotificationAsync، SendNewBookingNotificationAsync، ...) تصبح عمليّات أو sub-entries تُطلق من interceptors.

**Interceptors**:
- `NotifyOnChatMessageInterceptor` (المرحلة 8).
- `NotifyOnBookingStatusInterceptor` (المرحلة 5/6).
- `FCMDeliveryRetryInterceptor` — إعادة محاولة عند فشل عابر.

**معايير القبول**:
- تسجيل جهاز → إرسال test → يصل push على الجهاز.
- Broadcast لـ 1000 مستخدم → يكتمل ≤ 30 ثانية مع لوج واضح.
- حذف token قديم عند استلام `NotRegistered` من FCM تلقائيّاً.

**Definition of Done**: إشعارات InApp + Push تعمل عبر Provider contracts.

**Commit**: `feat(ashare-v2-api): notifications — FCM + signalR + admin broadcast via OAM`.

### المرحلة 10 — الملفّات والتخزين (GCS / Alibaba OSS)

**المصدر**: `MediaController` (4 endpoints) + `IStorageProvider`.

**Provider contract**:
```csharp
public interface IStorageProvider
{
    Task<UploadResult> UploadAsync(Stream content, string directory, string fileName, string contentType, CancellationToken ct);
    Task<Stream> DownloadAsync(string directory, string fileName, CancellationToken ct);
    Task DeleteAsync(string directory, string fileName, CancellationToken ct);
    string GetPublicUrl(string directory, string fileName);
    string Name { get; }  // "gcs" | "alibaba-oss" | "local"
}
```
تنفيذات: `GcsStorageProvider`, `AlibabaOssStorageProvider`, `LocalDiskStorageProvider` (للتطوير). الاختيار عبر config.

**العمليّات**:
| القديم | الجديد |
|---|---|
| `POST /api/media/upload` | `files.upload` — Entry من User → FileEntity، `Execute` يستدعي `IStorageProvider.UploadAsync` |
| `POST /api/media/upload/multiple` | `files.upload.batch` |
| `GET /api/media/{directory}/{fileName}` | `files.download` — قد يكون pass-through عبر URL مباشر (`GetPublicUrl`) بدل stream |
| `GET /api/media/{directory}/{subDirectory}/{fileName}` | نفس `files.download` بمسارات متداخلة |

**Analyzers**:
- `FileTypeAnalyzer` — JPEG/PNG/GIF/WebP فقط.
- `FileSizeAnalyzer` — ≤10MB للواحد، ≤50MB للمجموعة.
- `AllowedDirectoryAnalyzer` — `listings`, `profiles`, `vendors`, `complaints` فقط.

**Interceptors**:
- `ImageCompressionInterceptor` (اختياريّ خلفيّ) — يُعيد ضغط الصور الكبيرة قبل التخزين.
- `VirusScanInterceptor` (اختياريّ عبر Provider contract لاحقاً).

**معايير القبول**:
- رفع صورة → URL مُولَّد يعود في envelope + الصورة متاحة عبر `files.download`.
- محاولة رفع PDF → رفض عبر `FileTypeAnalyzer`.
- تبديل `IStorageProvider` من GCS إلى OSS عبر config فقط — بدون تغيير كود.

**Definition of Done**: تخزين متعدّد المزوّدين عبر Provider contract + Analyzers سليمة.

**Commit**: `feat(ashare-v2-api): files + multi-storage provider (GCS/OSS) with analyzers`.

### المرحلة 11 — الشكاوى (Complaints)

**المصدر**: `ComplaintsController`.

**الكيانات**: `Complaint` + `ComplaintReply` + `ComplaintAttachment` (→ `FileEntity`).

**العمليّات**:
| القديم | الجديد |
|---|---|
| `GET /api/complaints` | `complaints.list.mine` (+ `admin.complaints.list` للإدارة) |
| `GET /api/complaints/{id}` | `complaints.get` |
| `POST /api/complaints` | `complaints.create` |
| `POST /api/complaints/{id}/replies` | `complaints.reply` |
| `POST /api/complaints/{id}/close` | `complaints.close` |
| `POST /api/complaints/{id}/assign` (admin) | `admin.complaints.assign` |

**Entry for `complaints.create`**:
```csharp
Entry.Create("complaint.create")
    .From($"User:{reporterId}", 1, ("role","reporter"))
    .To($"Complaint:{newId}",   1, ("role","created"))
    .Tag("target", targetRef)   // listing:{id} / user:{id} / booking:{id}
    .Tag("category", category).Tag("notify","admin").Tag("audit","true")
    .Analyze(new RequiredFieldAnalyzer("subject", () => subject))
    .Analyze(new RequiredFieldAnalyzer("content", () => content))
    .Analyze(new ContentLengthAnalyzer(min: 20, max: 5000))
    .Build();
```

**Interceptors**:
- `AuditInterceptor` تلقائيّاً على كلّ `complaints.*`.
- `NotifyAdminOnComplaintInterceptor` على `Tag("notify","admin")` → ينشر لمجموعة SignalR إداريّة.

**معايير القبول**:
- مستخدم يفتح شكوى → تظهر للإدارة في `admin.complaints.list`.
- الردّ يُشعِر الطرف الآخر.
- الإقفال يمنع ردوداً إضافيّة (Analyzer).

**Definition of Done**: دورة شكوى كاملة مع Audit + إشعار إدارة.

**Commit**: `feat(ashare-v2-api): complaints lifecycle via OAM + admin notification interceptor`.

### المرحلة 12 — واجهة الإدارة (Dashboard + Admin controllers)

**المصدر**: `DashboardController` + `AdminOrdersController` + `AdminListingsController` + `ReportsController` + باقي admin endpoints.

**العمليّات**:
| القديم | الجديد |
|---|---|
| `GET /api/dashboard/stats` | `admin.dashboard.stats` — يجمع KPIs (مستخدمون، إعلانات، حجوزات، إيرادات) |
| `GET /api/admin/orders` | `admin.orders.list` |
| `GET /api/admin/listings` | `admin.listings.list` (للمراجعة) |
| `POST /api/admin/orders/{id}/status` | `admin.orders.update.status` |
| `GET /api/reports/{name}` | `admin.reports.{name}` |
| `GET /api/audit-logs` | `admin.audit.list` |

**جميع هذه العمليّات تحمل `Tag("admin","true")` + `Tag("audit","true")`** → `AdminAuthorizationInterceptor` يفرض role، `AuditInterceptor` يُسجّل.

**التقارير**:
- `admin.reports.sales` — مبيعات/إيرادات حسب فترة.
- `admin.reports.users.growth` — نمو المستخدمين.
- `admin.reports.listings.activity` — نشاط الإعلانات.
- `admin.reports.bookings.funnel` — quote → create → confirm → cancel.
- `admin.reports.complaints.sla` — زمن استجابة الشكاوى.

**معايير القبول**:
- Admin يشاهد Dashboard بكلّ KPIs.
- غير Admin يحاول `admin.*` → 403 + envelope خطأ.
- كلّ admin operation تظهر في `admin.audit.list`.

**Definition of Done**: لوحة إدارة كاملة عبر OAM مع authorization + audit.

**Commit**: `feat(ashare-v2-api): admin endpoints + dashboard + reports with audit`.

### المرحلة 13 — العرضيّات (Marketing/Attribution + AuditLog + ErrorReporting + LegalPages + AppVersions)

**1. التسويق والإسناد** (من `AttributionController` + `MarketingStatsController`):

| القديم | الجديد |
|---|---|
| `POST /api/marketing/attribution` | `marketing.attribution.capture` |
| `POST /api/marketing/attribution/associate` | `marketing.attribution.associate` |
| `GET /api/marketing/attribution/{sessionId}` | `marketing.attribution.get` |
| `GET /api/marketing/stats` | `admin.marketing.stats` |

**Entry for `marketing.attribution.capture`**:
```csharp
Entry.Create("marketing.attribution.capture")
    .From($"Session:{sessionId}", 1, ("role","visitor"))
    .To($"Attribution:{recordId}", 1, ("role","record"))
    .Tag("utmSource", src).Tag("utmMedium", med).Tag("utmCampaign", cam)
    .Tag("clickId", clickId).Tag("referrer", referrer)
    .Build();
```

**Interceptors**:
- `AttributionEnrichmentInterceptor` — يضيف بيانات من HTTP headers (User-Agent, Referer).
- `MetaConversionsInterceptor` — يُرسل إلى Meta Pixel API للأحداث المناسبة.

**2. AuditLog** (من `AuditLogController`):
- تشغيل `AuditInterceptor` عالميّاً على كلّ عمليّة تحمل `Tag("audit","true")`.
- `admin.audit.list(filter)` لعرض السجلّ.

**3. ErrorReporting** (من `ErrorReportingController`):

| القديم | الجديد |
|---|---|
| `POST /api/errorreporting/report` | `errors.report` |

**Provider contract**: `IErrorReportSink` — تنفيذ `SmtpErrorReportSink` (نسخ للـ email) + الإضافة المستقبلية لـ Sentry/DataDog بدون لمس المتحكّم.

**4. LegalPages** (من `LegalPagesController`):

| القديم | الجديد |
|---|---|
| `GET /api/legal/{key}` | `legal.page.get` |
| `GET /api/legal` | `legal.pages.list` |
| `POST /api/legal` (admin) | `admin.legal.upsert` |

**5. AppVersions** (من `AppVersionsController`):

| القديم | الجديد |
|---|---|
| `GET /api/versions/latest?platform={p}` | `app.versions.latest` |
| `POST /api/versions` (admin) | `admin.app.versions.publish` |
| `GET /api/versions` (admin) | `admin.app.versions.list` |

**6. DocumentTypes** (`DocumentTypesController`): عمليّات `document-types.list|create|update`.

**معايير القبول**:
- حدث تسجيل جديد → يظهر في `marketing.attribution.list` مع UTM صحيحة.
- خطأ من التطبيق → يصل email + يُسجَّل في logs.
- Admin يُحدِّث صفحة قانونيّة → تظهر في الواجهة فوراً.

**Definition of Done**: كلّ العرضيّات تعمل كعمليّات OAM عاديّة.

**Commit**: `feat(ashare-v2-api): marketing + audit + errors + legal + versions via OAM`.

### المرحلة 14 — البذور والترحيل من الإنتاج

**المصدر**: `AshareSeedDataService` + `OffersMigrationService` + `MigrationController`.

**التصميم وفق SEEDING.md**:
- `AshareSeeder : IHostedService` — idempotent (check-by-ID)، يعمل عند الإقلاع.
- بذور منسَّقة (SEEDING.md:24-36): مُعرِّفات ثابتة مشتركة عبر الخدمات:
  ```csharp
  public static class AshareSeedIds
  {
      public static readonly Guid CategoryResidential      = Guid.Parse("00000000-0000-0000-0001-000000000001");
      public static readonly Guid CategoryLookingForHousing= Guid.Parse("00000000-0000-0000-0001-000000000002");
      public static readonly Guid CategoryLookingForPartner= Guid.Parse("00000000-0000-0000-0001-000000000003");
      public static readonly Guid CategoryAdministrative   = Guid.Parse("00000000-0000-0000-0001-000000000004");
      public static readonly Guid CategoryCommercial       = Guid.Parse("00000000-0000-0000-0001-000000000005");
      // Attribute definitions, Plans, Admin user...
  }
  ```
- البذور الأساسيّة: 5 فئات + تعريفات السمات (Common + Residential + Partner + Commercial) + خطط الاشتراك + Admin user + LegalPages stubs.

**البذر من الإنتاج** (SEEDING.md:137-142):
- `AshareSeeder` عند بدء التطبيق يستدعي عمليّة محلّية `seed.from-production`:
  - يجلب قوائم حقيقيّة من API القديم (`ashare-001-site4.mtempurl.com` أو نسخته الحاليّة).
  - يُدخِلها كـ ProductListings مع ربطها بفئات البذور المحلّية.
  - dedupe by ID (idempotent).
- أمان `JsonElement` (SEEDING.md:176-179): `TryGetProperty` يرمي على array elements — `ValueKind != Object` check أوّلاً.
- تحميل الصور: عبر `IStorageProvider` (GCS/OSS) من `ashare-001-site6.mtempurl.com/Images/`.
- Fallback للبيانات الثابتة إن فشل الإنتاج — لا يُفشَل البذر.

**العمليّات الإداريّة**:
| القديم | الجديد |
|---|---|
| `POST /api/migration/offers/seed` | `admin.migration.offers.seed` |
| `POST /api/migration/offers/reseed` | `admin.migration.offers.reseed` (حذف وإعادة بذر مع صور) |

**Entry للـ seeding**:
```csharp
Entry.Create("seed.from-production")
    .From("System:Seeder", 1, ("role","seeder"))
    .To("System:DB", count, ("role","recipient"))
    .Tag("sealed","true")   // منع audit/quota interceptors
    .Execute(async ctx => { ... })
    .Build();
```

**معايير القبول**:
- أوّل إقلاع يُنشئ الفئات + السمات + الخطط + Admin.
- إقلاع ثانٍ بدون تكرار (idempotent).
- الترحيل من الإنتاج يجلب N listings صحيحة مع صورها.
- الإنتاج غير متاح → fallback للبيانات الثابتة يعمل، لا فشل إقلاع.

**Definition of Done**: قاعدة بيانات مُبَذَّرة قابلة للاستخدام فوراً من الواجهة.

**Commit**: `feat(ashare-v2-api): seeding + production migration with coordinated IDs`.

### المرحلة 15 — الملاحظة والتشغيل (Observability)

**المكوّنات**:
- **Serilog**: نفس configuration القديم + enrichment بـ `OperationType` و `EnvelopeSuccess` تلقائيّاً من interceptor.
- **Health checks**: `/health` (liveness) + `/health/ready` (readiness — يفحص DB + Redis + Storage + Firebase).
- **Swagger/OpenAPI**: كلّ `Op` يُوثَّق تلقائيّاً من `OperationEnvelope<T>` schema.
- **Metrics** (اختياريّ): Prometheus counter لكلّ `operation.type` + histogram لـ latency.
- **Rate-limit interceptor** (LIBRARY-ANATOMY.md:148-155): على `Tag("rate-limited","true")` — يُطبَّق على `auth.nafath.initiate` و`files.upload` و`complaints.create`.

**Service Registry**:
- `ACommerce.ServiceRegistry.Client` — يُسجَّل كما في القديم (نفس نمط التسجيل المركزيّ بين الخدمات).

**GlobalExceptionMiddleware**:
- أيّ استثناء غير مُعالَج → `OperationEnvelope<object>` مع `success:false` + `errorCode` (محدَّد) + `errorMessage` (للمستخدم) + `traceId`.
- `SignalRExceptionFilter` — نفس المنطق للـ hubs.

**معايير القبول**:
- `/health` أخضر مع كلّ المكوّنات.
- log entry واحد لكلّ عمليّة مع `operationId`, `type`, `latencyMs`, `success`.
- Prometheus scrape يعود بقياسات صحيحة.

**Definition of Done**: خدمة قابلة للنشر الإنتاجيّ مع observability سليمة.

**Commit**: `feat(ashare-v2-api): observability — serilog + health + swagger + rate-limit`.

---

## 2. مصفوفة معايير القبول الخلفيّة

| المعيار | الفحص | العتبة | المرجع |
|---|---|---|---|
| Envelope coverage | grep: `return Ok(` خارج `OkEnvelope` | 0 | CLAUDE.md:56-58 |
| No direct mutation | grep: `_repo.(Add\|Update\|Delete)Async` خارج `.Execute(` | 0 | CLAUDE.md:41-53 |
| Entry validity | فحص `AccountingBuilder` في كلّ `Entry.Create(...).Build()` يشمل `From+To+Balance` | يمرّ | MODEL.md:65-82 |
| Repo signatures | `ListAllAsync` تأخذ `CancellationToken`؛ `GetAllWithPredicateAsync` لا تأخذها | صحيح لكلّ استخدام | CLAUDE.md:60-64, BUILDING-A-BACKEND.md:286-289 |
| Provider contracts | كلّ اعتماد خارجيّ (Nafath/Noon/Firebase/Storage/SMTP) عبر واجهة `.Requires<T>()` | لا تنفيذ مباشر في المكتبة | MODEL.md:125-142, LIBRARY-ANATOMY.md:69-127 |
| Interceptors registered | كلّ Tag مستخدَم له معترض أو لا يحتاج — لا Tag ميّت | يمرّ | LIBRARY-ANATOMY.md:128-155 |
| Culture stack | `UseCultureContext()` بين Routing والـ controllers + `NumeralToLatinSaveInterceptor` نشط | يمرّ | CULTURE.md:42-49, 33-34 |
| DynamicAttributes | سمات غير معروفة تُحفَظ خامّة، لا تُرفض | يمرّ في integration test | DYNAMIC-ATTRIBUTES.md:47-54, CLAUDE.md:76-85 |
| Seeding idempotency | إقلاع ثانٍ لا يُضاعف البيانات | يمرّ | SEEDING.md:24-36 |
| Tests | `dotnet test` يغطّي كلّ operation بـ unit + integration | ≥ 80% coverage (target) | — |
| Swagger completeness | كلّ endpoint موثَّق + schema للـ envelope | يمرّ | — |

**سكربتات فحص مقترَحة في `scripts/`** (تُضاف عند الحاجة):
- `scripts/verify-backend-envelope.sh` — grep على المتحكّمات.
- `scripts/verify-backend-mutations.sh` — grep على `_repo.*Async` خارج `.Execute`.
- `scripts/verify-backend-entries.sh` — تحليل AST للتحقّق من `From+To` في كلّ `Entry.Create`.

---

## 3. فهرس الاستشهادات المنهجيّة

كلّ مرجع له نصّ بالحرف في `docs/ASHARE-METHODOLOGY-CITATIONS.md` مع `file:line`:

- **CLAUDE.md** — القوانين الستّة (41-85) — حكم كلّ طفرة حالة.
- **MODEL.md** — بنية الـ Entry (65-82)، Analyzer vs Interceptor (83-123)، Sealed/Exclude (122-123)، ProviderContract (125-142).
- **LIBRARY-ANATOMY.md** — ثلاثيّة الطبقات (33-44, 69-127, 128-155) — نموذج بناء مكتبة Ashare العاموديّة.
- **BUILDING-A-BACKEND.md** — تسجيل الكيانات (112-115)، OpEngine (135-136)، قواعد Repository (286-289)، نمط Controller للطفرات (295-350)، نمط Seeder (388-410).
- **CULTURE.md** — `AddCultureStack()` + `UseCultureContext()` (42-49)، `NumeralToLatinSaveInterceptor` (33-34).
- **SEEDING.md** — بذور منسَّقة (24-36)، البذر من الإنتاج (137-142)، أمان JsonElement (176-179).
- **DYNAMIC-ATTRIBUTES.md** — Template + Snapshot (13-20)، مفاتيح غير معروفة (47-54).
- **VERIFICATION-LAYERS.md** — الطبقات الستّ (86-119) — تُطبَّق خلفيّاً على الـ API من جهة الواجهة.
- **ASHARE-V2-METHODOLOGY.md** — نمط `Tag("client_dispatch","true")` + `UserCulture` (236-304).

---

## 4. سجلّ التغييرات

- **2026-04-18** — النسخة الأولى من الخطّة: 16 مرحلة + مصفوفة قبول + فهرس استشهادات + 5 ملاحق.

---

## ملحق أ — جدول الـ endpoints الكامل (71 endpoint)

### أ-1. المتحكّمات المحلّية (10 controllers / 48 endpoints)

| # | HTTP | المسار القديم | العمليّة الجديدة | المرحلة |
|--:|---|---|---|--:|
| 1 | POST | `/api/auth/nafath/initiate` | `auth.nafath.initiate` | 1 |
| 2 | GET | `/api/auth/nafath/status` | `auth.nafath.status` | 1 |
| 3 | POST | `/api/auth/nafath/complete` | `auth.nafath.complete` | 1 |
| 4 | POST | `/api/auth/admin/login` | `auth.admin.login` | 1 |
| 5 | GET | `/api/auth/me` | `auth.me` | 1 |
| 6 | POST | `/api/auth/logout` | `auth.logout` | 1 |
| 7 | POST | `/api/auth/nafath/webhook` | `auth.nafath.webhook` | 1 |
| 8 | POST | `/api/notifications/device-token` | `notifications.device.register` | 9 |
| 9 | DELETE | `/api/notifications/device-token` | `notifications.device.revoke` | 9 |
| 10 | GET | `/api/notifications/devices/count` | `notifications.devices.count` | 9 |
| 11 | POST | `/api/notifications/test` | `notifications.test` | 9 |
| 12 | GET | `/api/notifications/settings` | `notifications.settings.get` | 9 |
| 13 | PUT | `/api/notifications/settings` | `notifications.settings.update` | 9 |
| 14 | GET | `/api/admin/notifications/users` | `admin.notifications.users.list` | 9 |
| 15 | POST | `/api/admin/notifications/send` | `admin.notifications.send` | 9 |
| 16 | POST | `/api/admin/notifications/broadcast` | `admin.notifications.broadcast` | 9 |
| 17 | GET | `/api/admin/notifications/stats` | `admin.notifications.stats` | 9 |
| 18 | GET | `/api/admin/notifications/test` | يُحذَف (diagnostic) | — |
| 19 | POST | `/api/admin/notifications/test-firebase` | يُحذَف | — |
| 20 | POST | `/api/admin/notifications/test-firebase-direct` | يُحذَف | — |
| 21 | POST | `/api/media/upload` | `files.upload` | 10 |
| 22 | POST | `/api/media/upload/multiple` | `files.upload.batch` | 10 |
| 23 | GET | `/api/media/{directory}/{fileName}` | `files.download` | 10 |
| 24 | GET | `/api/media/{directory}/{sub}/{fileName}` | `files.download` (nested) | 10 |
| 25 | GET | `/host/payment/callback` | `payments.callback.process` | 6 |
| 26 | POST | `/host/payment/callback` | `payments.callback.process` | 6 |
| 27 | POST | `/api/errorreporting/report` | `errors.report` | 13 |
| 28 | POST | `/api/marketing/attribution` | `marketing.attribution.capture` | 13 |
| 29 | POST | `/api/marketing/attribution/associate` | `marketing.attribution.associate` | 13 |
| 30 | GET | `/api/marketing/attribution/{sessionId}` | `marketing.attribution.get` | 13 |
| 31 | GET | `/api/categoryattributes/mappings` | `categories.mappings.list` | 3 |
| 32 | GET | `/api/categoryattributes/category/{id}` | `categories.attributes.get` | 3 |
| 33 | GET | `/api/categoryattributes/categories` | `categories.list` | 3 |
| 34 | GET | `/api/categoryattributes/debug/all-attributes` | يُحذَف (debug) | — |
| 35 | POST | `/api/migration/offers/seed` | `admin.migration.offers.seed` | 14 |
| 36 | POST | `/api/migration/offers/reseed` | `admin.migration.offers.reseed` | 14 |

### أ-2. المتحكّمات من مكتبة ACommerce (23 controllers — يبقى مسارها، تُحوَّل إلى عمليّات)

| المتحكّم | الـ Route Prefix | العمليّات الرئيسيّة | المرحلة |
|---|---|---|--:|
| `ProfilesController` | `/api/profiles/*` | `profiles.me`, `profiles.get`, `profiles.update`, `profiles.avatar.update` | 2 |
| `VendorsController` | `/api/vendors/*` | `vendors.list`, `vendors.get`, `vendors.create`, `vendors.update` | 2 |
| `ProductsController` | `/api/products/*` | `products.list`, `products.get`, `products.create`, `products.update` | 4 |
| `ProductListingsController` | `/api/listings/*` | `listings.featured`, `listings.new`, `listings.list`, `listings.search`, `listings.get`, `listings.create`, `listings.update`, `listings.delete` | 4 |
| `OrdersController` | `/api/orders/*` | `orders.list.mine`, `orders.get`, `orders.create` | 5/6 |
| `PaymentsController` | `/api/payments/*` | `payments.initiate`, `payments.get`, `payments.refund` | 6 |
| `ChatsController` | `/api/chats/*` + `/hubs/chat` | `chats.list.mine`, `chats.start`, `chats.messages.list`, `chats.message.send`, `chats.read` | 8 |
| `BookingsController` | `/api/bookings/*` | `bookings.list.mine`, `bookings.get`, `bookings.quote`, `bookings.create`, `bookings.confirm`, `bookings.reject`, `bookings.cancel` | 5 |
| `DashboardController` | `/api/dashboard/*` | `admin.dashboard.stats` | 12 |
| `AdminOrdersController` | `/api/admin/orders/*` | `admin.orders.list`, `admin.orders.update.status` | 12 |
| `AdminListingsController` | `/api/admin/listings/*` | `admin.listings.list`, `admin.listings.moderate` | 12 |
| `ReportsController` | `/api/reports/*` | `admin.reports.{name}` | 12 |
| `ComplaintsController` | `/api/complaints/*` | `complaints.list.mine`, `complaints.get`, `complaints.create`, `complaints.reply`, `complaints.close` | 11 |
| `SubscriptionsController` | `/api/subscriptions/*` | `subscriptions.plans.list`, `subscriptions.checkout`, `subscriptions.current`, `subscriptions.cancel` | 7 |
| `DocumentTypesController` | `/api/document-types/*` | `document-types.list|create|update` | 13 |
| `LocationSearchController` | `/api/locations/*` | `locations.search` | 3 |
| `AttributeDefinitionsController` | `/api/attributes/*` | `attributes.list|create|update` | 3 |
| `UnitsController` | `/api/units/*` | `units.list` | 3 |
| `CurrenciesController` | `/api/currencies/*` | `currencies.list` | 3 |
| `ContactPointsController` | `/api/contact-points/*` | `contact-points.list|create|update` | 3 |
| `AppVersionsController` | `/api/versions/*` | `app.versions.latest`, `admin.app.versions.publish`, `admin.app.versions.list` | 13 |
| `AuditLogController` | `/api/audit-logs/*` | `admin.audit.list` | 12 |
| `LegalPagesController` | `/api/legal/*` | `legal.page.get`, `legal.pages.list`, `admin.legal.upsert` | 13 |
| `MarketingStatsController` | `/api/marketing/stats/*` | `admin.marketing.stats` | 13 |

### أ-3. الـ SignalR Hubs (2)

| Hub | المسار | الدور الجديد |
|---|---|---|
| `NotificationHub` | `/hubs/notifications` | ينضمّ المستخدم بـ JWT، الـ hub يستقبل `IRealtimeMessageBus` → يوزّع على clients. لا طفرات فيه. |
| `ChatHub` | `/hubs/chat` | نفس المبدأ — `chats.message.send` تُنفَّذ عبر `OpEngine` من REST، الـ hub للتبادل الفوريّ فقط. |

---

## ملحق ب — Provider Contracts كاملة

كلّ اعتماد خارجيّ مُحظَّم بواجهة `IXxxProvider`؛ المكتبة العموديّة تشحن بصفر تنفيذ، والتطبيق المستهلك يحقن المناسب (MODEL.md:125-142, LIBRARY-ANATOMY.md:69-127).

| # | Contract | الدور | تنفيذ الإنتاج | تنفيذ التطوير | يُستخدم في مرحلة |
|--:|---|---|---|---|--:|
| 1 | `ITwoFactorAuthenticationProvider` | المصادقة الثنائيّة عبر نفاذ | `NafathTwoFactorProvider` | `FakeTwoFactorProvider` (accept-all) | 1 |
| 2 | `ITwoFactorSessionStore` | حفظ جلسات 2FA المعلَّقة | `EfTwoFactorSessionStore` / `RedisTwoFactorSessionStore` | `InMemoryTwoFactorSessionStore` | 1 |
| 3 | `IAuthenticationProvider` | إصدار JWT + Admin password | `JwtAuthenticationProvider` | نفسه + keys محلّية | 1 |
| 4 | `IPaymentGateway` | بوّابة الدفع | `NoonPaymentGateway` | `FakePaymentGateway` (يُنجح فوراً) | 6 |
| 5 | `IPushNotificationProvider` | إرسال push | `FirebasePushNotificationProvider` | `ConsolePushNotificationProvider` | 9 |
| 6 | `IFirebaseTokenStore` | تخزين device tokens | `EfFirebaseTokenStore` | نفسه | 9 |
| 7 | `IRealtimeMessageBus` | نشر فوريّ | `SignalRMessageBus` | نفسه + Redis اختياريّ | 8, 9 |
| 8 | `IStorageProvider` | تخزين الملفّات | `GcsStorageProvider` / `AlibabaOssStorageProvider` | `LocalDiskStorageProvider` | 10 |
| 9 | `IErrorReportSink` | استقبال تقارير الأخطاء | `SmtpErrorReportSink` (+ Sentry/DataDog لاحقاً) | `ConsoleErrorReportSink` | 13 |
| 10 | `IMarketingEventTracker` | حدث تسويقيّ (registration/login) | `CompositeEventTracker(Meta, GA, ...)` | `ConsoleEventTracker` | 1, 13 |
| 11 | `IAttributionEnrichmentProvider` | إثراء بيانات UTM من HTTP | `HttpAttributionEnrichmentProvider` | نفسه | 13 |
| 12 | `ILegalPageContentProvider` | مصدر محتوى الصفحات القانونيّة (Markdown/HTML) | `DbLegalPageContentProvider` | نفسه | 13 |
| 13 | `IAppVersionPublisher` | إصدار نسخ جديدة للأجهزة | `DbAppVersionPublisher` | نفسه | 13 |
| 14 | `ILocationSearchProvider` | بحث جغرافيّ | `DbLocationSearchProvider` (مع Google Geocoding لاحقاً) | نفسه | 3 |
| 15 | `IContentFilterProvider` | تنقية نصوص | `RegexContentFilter` (قائمة ممنوعات) أو `AiContentFilter` | `NoopContentFilter` | 4 |

**قاعدة**: إن أضاف مطوّر اعتماداً خارجيّاً جديداً في متحكّم (HTTP client، SMTP، sms, ...)، يجب أن يُعرَّف كـ Provider contract أوّلاً ويحقن. لا HTTP clients مباشرة من المتحكّم.

---

## ملحق ج — Interceptors كاملة

الـ Interceptor معترض عابر يُسجَّل في DI ويُطبَّق تلقائيّاً عند مطابقة الوسوم على عمليّة (MODEL.md:83-123, LIBRARY-ANATOMY.md:128-155).

| # | Interceptor | يُطلَق بواسطة Tag | الفعل | المرحلة |
|--:|---|---|---|--:|
| 1 | `QuotaInterceptor` | `quota_check=true` + tag نوعيّ (`listings`, `bookings`) | يفحص الحدود قبل التنفيذ، يرفض مع envelope خطأ إن تجاوز | 4, 7 |
| 2 | `AuditInterceptor` | `audit=true` | يُدخل قيداً في `AuditLog` بعد التنفيذ (نجاح أو فشل) | كلّ المراحل الحسّاسة |
| 3 | `CacheInterceptor` | `cacheable=true` | يلفّ القراءات في cache بمفتاح مشتقّ من الوسوم؛ يُبطل عند العمليّات المرتبطة | 4 |
| 4 | `ContentFilterInterceptor` | `content_check=true` | يستدعي `IContentFilterProvider` على حقول النصّ (title, description, message) | 4, 8, 11 |
| 5 | `NotifyOnBookingInterceptor` | `notify=booking` | يُطلق sub-entry `notifications.dispatch.booking.{status}` | 5, 6 |
| 6 | `NotifyOnChatMessageInterceptor` | `notify=chat` | ينشر للعميل عبر `IRealtimeMessageBus` + Push إن offline | 8 |
| 7 | `NotifyAdminOnComplaintInterceptor` | `notify=admin` | ينشر لمجموعة إداريّة | 11 |
| 8 | `MarketingEventInterceptor` | `marketing=*` (registration/login/purchase) | يستدعي `IMarketingEventTracker` | 1, 13 |
| 9 | `AttributionEnrichmentInterceptor` | `attribution=capture` | يُضيف UTM headers قبل الحفظ | 13 |
| 10 | `PaymentCaptureOnSuccessInterceptor` | `payment=callback` + `status=success` | يُطلق sub-entry `bookings.confirm` أو `subscriptions.activate` | 6 |
| 11 | `RenewalReminderInterceptor` | يُجدوَل زمنيّاً (HostedService) | يُذكّر المستخدمين قبل انتهاء الاشتراك | 7 |
| 12 | `RateLimitInterceptor` | `rate-limited=true` | يُطبَّق على `auth.nafath.initiate`, `files.upload`, `complaints.create` | 15 |
| 13 | `AdminAuthorizationInterceptor` | `admin=true` | يتحقّق من role قبل التنفيذ | 12 |
| 14 | `NumeralToLatinSaveInterceptor` | عامّ — عند حفظ حقول نصّيّة تحوي أرقام | يحوّل الأرقام الهنديّة/الفارسيّة إلى لاتينيّة قبل DB (CULTURE.md:33-34) | كلّ المراحل |
| 15 | `CultureInterceptor` | على كلّ استجابة | يُضيف headers `Content-Language` / `Currency` (ASHARE-V2-METHODOLOGY.md:236-304) | كلّ المراحل |
| 16 | `FCMDeliveryRetryInterceptor` | `push_retry=true` | إعادة محاولة عند فشل عابر؛ يُعطّل token عند `NotRegistered` | 9 |

**قواعد**:
- `entry.Sealed()` يمنع كلّ الـ interceptors (مثلاً `seed.from-production`).
- `entry.ExcludeInterceptor("Name")` يمنع معترضاً واحداً باسمه.
- لا interceptor يُدخل طفرة في DB مباشرة — إن احتاج ذلك يُنشئ sub-entry.

---

## ملحق د — Seeder + Migration من الإنتاج (تفصيل)

### د-1. البذور الأساسيّة (Static Seed)

مستنسَخة من `AshareSeedDataService` القديمة، مُنسَّقة IDs (SEEDING.md:24-36):

**الفئات الخمس** (`ProductCategory`):
| ID (GUID) | Key | Name AR | Name EN |
|---|---|---|---|
| `...0001-000000000001` | `Residential` | سكني | Residential |
| `...0001-000000000002` | `LookingForHousing` | طلب سكن | Looking For Housing |
| `...0001-000000000003` | `LookingForPartner` | طلب شريك | Looking For Partner |
| `...0001-000000000004` | `Administrative` | إداري | Administrative |
| `...0001-000000000005` | `Commercial` | تجاري | Commercial |

**تعريفات السمات** (`AttributeDefinition`) — مجموعات:
- **Common**: Title, Description, Price, Duration, TimeUnit, Location, City, Images.
- **Residential**: PropertyType, UnitType, Floor, BillType, RentalType, Area, Rooms, Bathrooms, Furnished, Amenities, ContactPreferences.
- **LookingForPartner**: PersonalName, Age, Gender, Nationality, Job, MinPrice, MaxPrice, Smoking.
- **Commercial**: CommercialPropertyType, Capacity, Parking, WorkingHours, Facilities.

**CategoryAttributeMapping**: يربط كلّ فئة بسماتها المطلوبة والاختياريّة.

**خطط الاشتراك** (`SubscriptionPlan`): مستخرَجة من `AshareSubscriptionPlans` (في الـ Shared القديم) — Basic / Pro / Premium مع limits.

**Admin user** واحد للإقلاع الأوّل (id ثابت، password يُقرأ من secret).

**LegalPages stubs**: terms, privacy, cookies — تُملأ لاحقاً.

### د-2. الترحيل من الإنتاج (Production Migration)

مستنسَخ من `OffersMigrationService`:

**الخطوات**:
1. عند إقلاع `AshareSeeder`، إن كان `Config:Migration:EnableProductionSeed == true`:
   - `FetchOffersListAsync()` → يستدعي `{ProductionBaseUrl}/api/offers` (مثل `ashare-001-site4.mtempurl.com`).
   - لكلّ offer: `FetchOfferDetailsAsync(id)`.
   - لكلّ صورة في التفاصيل: تحميل من `{ImagesBaseUrl}/Images/{name}` → رفع إلى `IStorageProvider` → استبدال الـ URL.
   - إنشاء `Product` + `ProductListing` + ربط الفئة المناسبة.
   - Dedupe by external ID (حقل `ExternalId` في Listing).

**أمان JsonElement** (SEEDING.md:176-179):
```csharp
foreach (var offer in offers.EnumerateArray())
{
    if (offer.ValueKind != JsonValueKind.Object) continue;   // يتخطّى array elements
    if (!offer.TryGetProperty("id", out var idProp)) continue;
    // ...
}
```

**Fallback**:
- إن فشل الجلب من الإنتاج (timeout / 5xx) → `SeedOffersFromStaticDataAsync()` يقرأ من ملفّ JSON محلّيّ (نسخة مجمَّدة من الإنتاج).
- لا يُفشَل إقلاع الخدمة — فقط log warning.

### د-3. إدارة يدويّة للإعادة

`admin.migration.offers.reseed` يحذف كلّ الـ listings الممسَّنة من الإنتاج (حيث `Source == "production-migrate"`) ويعيد البذر. محميّة بـ Admin role + Audit.

### د-4. Hosted Service للتجديد

`ProductionRefreshHostedService` (اختياريّ، يُؤجَّل): كلّ N ساعة يُعيد جلب الـ offers الجديدة (بدون حذف) لمواكبة الإنتاج.

---

## ملحق هـ — مراجع متقاطعة + DoD موحَّد

### هـ-1. المستندات الداخليّة ذات الصلة

| المستند | ما يستخدَم منه |
|---|---|
| `ASHARE-LEGACY-INVENTORY.md` | المصدر المباشر لجرد الـ endpoints (10+23+2+1) |
| `ASHARE-METHODOLOGY-CITATIONS.md` | الاستشهادات بـ `file:line` لكلّ قاعدة منهجيّة |
| `BUILDING-A-BACKEND.md` | وصفة بناء الخدمة الخلفيّة — الوصفة الأساس للمرحلة 0 وما بعدها |
| `MODEL.md` | تعريف Entry, Analyzer, Interceptor, ProviderContract, Sealed, ExcludeInterceptor |
| `LIBRARY-ANATOMY.md` | ثلاثيّة الطبقات — إن قُرِّر تجريد عشير كمكتبة عموديّة `ACommerce.Ashare` |
| `CULTURE.md` | حزمة الثقافة الخلفيّة — المرحلة 0 + النمط 14 في ملحق ج |
| `SEEDING.md` | Seeder pattern + production backfill + JsonElement safety — المرحلة 14 + ملحق د |
| `DYNAMIC-ATTRIBUTES.md` | Template + Snapshot + preserve unknown — المراحل 3, 4 |
| `ROADMAP.md` | وسم التقدّم بعد كلّ مرحلة |
| `Apps/Order.Api/*` | مرجع تطبيقيّ أنظف مثال (cafe deals) — يُقرأ عند الحاجة |
| `Apps/Vendor.Api/*` | مرجع لـ microservice مستقلّ |
| `Apps/Ashare.Api/*` (V1 في المستودع الحاليّ) | **مرجع تطبيقيّ فقط** — لا يُنسَخ؛ يُقرأ لمعرفة «كيف طُبّقت المنهجيّة في حالة مشابهة» |

### هـ-2. Definition of Done موحَّد لكلّ PR من هذه الخطّة

كلّ PR من هذه الخطّة يجب أن يحوي:

1. **كود المرحلة** (controllers + operations + analyzers + interceptors + provider contracts + تنفيذات Fake للتطوير).
2. **اختبارات وحدة**: كلّ Entry/Analyzer/Interceptor له اختبار.
3. **اختبارات تكامليّة**: كلّ endpoint يُختبَر مع `WebApplicationFactory` — يُتحقَّق من:
   - `OperationEnvelope<T>` شكليّاً.
   - مسار النجاح.
   - مسار الفشل (Analyzer أو Authorization).
4. **تحديث `Program.cs`** بتسجيل الكيانات + الخدمات + الـ interceptors الجديدة.
5. **تحديث `ROADMAP.md`** بوسم المرحلة [x].
6. **نتائج الفحص** مُلصقة في وصف الـ PR:
   ```
   $ dotnet build        → 0 warnings
   $ dotnet test         → N/N passed
   $ grep -r "return Ok(" src/ | grep -v "OkEnvelope" → 0
   $ grep -r "_repo\.\(Add\|Update\|Delete\)Async" src/ | grep -v "Execute(ctx" → 0
   ```
7. **Migration جديد** (إن لزم) + تعليمات تطبيقه.
8. **Swagger snapshot** يُحدَّث تلقائيّاً.
9. **قسم «Methodology Compliance»** في وصف الـ PR يربط كلّ ملفّ جديد بالقانون/المستند.

### هـ-3. ترتيب المراحل وتبعيّاتها

```
0 (Bootstrap)
└── 1 (Auth) ────┬── 2 (Profiles) ─── 3 (Catalog) ─── 4 (Listings) ─── 5 (Bookings) ─── 6 (Payments) ─── 7 (Subscriptions)
                 │
                 ├── 8 (Chats) ─── 9 (Notifications)
                 │
                 ├── 10 (Files)
                 │
                 ├── 11 (Complaints)
                 │
                 ├── 12 (Admin)
                 │
                 ├── 13 (Cross-cutting)
                 │
                 └── 14 (Seeding) ─── 15 (Observability)
```

المراحل 8 و10 و11 و13 يمكن أن تُنفَّذ بالتوازي بعد اكتمال 1-7.

### هـ-4. مقاييس النجاح النهائيّ (Production-readiness)

- تغطية اختبارات ≥ 80%.
- كلّ الـ endpoints القديمة (71) لها عمليّات OAM مكافئة.
- grep على كلّ المشروع: 0 انتهاكات للقوانين الستّ.
- خدمة تعمل 48 ساعة متواصلة في staging بدون خطأ.
- Load test: 1000 RPS على `listings.list` مع p95 < 200ms.
- Firebase + Noon + GCS/OSS + SignalR كلّها متّصلة بالإنتاج ومُختبَرة.
