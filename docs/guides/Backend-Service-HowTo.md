# How to create a backend API service in this repository

هذا الدليل يشرح خطوة بخطوة كيفية إنشاء خدمة خلفية (API) في هذا المستودع باستخدام المكتبات المشتركة (Shared Kernel) وبأقل قدر من التعديلات على المكتبات الأساسية.

A short English summary is provided as well.

---

## 1) إنشاء المشروع

1. في المسار المناسب (مثال: `Apps/MyService/MyService.Api`) استخدم قالب Web API:
   - إنشاء مجلد المشروع وملف المشروع (.csproj) بنفس شكل مشاريع API في `Apps/*/*Api`.
   - استهدف `net9.0` وفعّل `Nullable` و`ImplicitUsings` لتوافق القاعدة.

Example csproj skeleton (adjust paths):

- Add ProjectReferences:
  - `libs/backend/core/ACommerce.SharedKernel.Abstractions` (IBaseEntity, etc.)
  - `libs/backend/core/ACommerce.SharedKernel.Infrastructure.EFCores` (ApplicationDbContext + EF helpers)
  - `libs/backend/integration/ACommerce.SharedKernel.AspNetCore` (optional helper extensions)
  - `libs/backend/integration/ACommerce.Authentication.AspNetCore.Swagger` (recommended for OpenAPI)

2) إذا كانت لديك كائنات مجال مشتركة بين خدمات، انشئ مشروع `Shared.Domain` تحت `Apps/YourApp/Shared` وأضفه كمراجع (ProjectReference) إلى كل Api يحتاجها.

---

## 2) تسجيل الكيانات (Entity discovery)

- قبل أي إنشاء أو استخدام لـ `ApplicationDbContext` يجب تسجيل الكيانات المخصصة صراحةً باستخدام:

  ACommerce.SharedKernel.Abstractions.Entities.EntityDiscoveryRegistry.RegisterEntity<MyEntity>();

- ضع هذا السطر مبكراً في `Program.cs`, قبل `var app = builder.Build()` أو قبل أي استدعاء يسبب بناء نموذج EF.

---

## 3) تكوين DbContext و DI

في `Program.cs` اتبع هذا النمط:

- استخدم المساعدات الجاهزة للتطوير المحلي:
  - `builder.Services.AddACommerceSQLite("Data Source=my.db")`  // يستخدم SQLite محلياً
  - أو `builder.Services.AddACommerceDbContext(options => options.UseSqlServer(connectionString))` عند الحاجة

- سجّل أي seed services: `builder.Services.AddScoped<MySeedService>();`

- بعد `var app = builder.Build()`:
  - استدعي `EntityDiscoveryRegistry.RegisterEntity<T>()` لكل كيان جديد
  - افتح scope واحصل على `ApplicationDbContext`, نفّذ `await dbContext.Database.EnsureCreatedAsync()` أو `MigrateAsync()` ثم نفّذ Seeder من خلال DI

نموذج مختصر:

## توضيحات مهمة وتصحيحات بعد التعديلات الأخيرة

هذا الملف الآن باللغة العربية فقط ويشرح كيفية بناء خدمة خلفية متوافقة مع المكتبات المشتركة في المستودع، بالإضافة إلى توضيح سبب ظهور صفحات السواجر بيضاء ولماذا كانت الخدمتان تبدوان "فارغتين" عند التشغيل.

ملاحظة سريعة: بعد التعديلات الأخيرة تم تضمين ACommerce Swagger وتفعيلها شرطياً. فيما يلي توضيحات أعمق لتفهم السبب وكيف تصلح ذلك عند بناء خدمة جديدة.

---

### لماذا كانت صفحات السواجر بيضاء (صفحة فارغة بدون مسارات)؟

السبب الأساسي: لا توجد Controllers مرئية لدى نظام MVC للتطبيق. في هذه المنظومة كثير من الـ Controllers موجودة في مكتبات/مشروعات أخرى (مثل مكتبات `Profiles`, `Orders`, `Chats`, `Notifications`، إلخ). لكي تَعرض السواجر هذه المسارات يجب أن تخبر ASP.NET Core أن يبحث عن الـ Controllers هذه في التجميعات (assemblies) الأخرى.

حل المشكلة: عند تهيئة MVC نستخدم:

```csharp
builder.Services.AddControllers()
    .AddApplicationPart(System.Reflection.Assembly.GetExecutingAssembly()) // controllers المحلية إن وُجدت
    .AddApplicationPart(typeof(ACommerce.Profiles.Api.Controllers.ProfilesController).Assembly)
    .AddApplicationPart(typeof(ACommerce.Orders.Api.Controllers.OrdersController).Assembly)
    .AddApplicationPart(typeof(ACommerce.Chats.Api.Controllers.ChatsController).Assembly)
    .AddApplicationPart(typeof(ACommerce.Notifications.Recipients.Api.Controllers.AdminNotificationsController).Assembly);
```

هذه الأسطر تخبر الإطار بتحميل الـ Controllers الموجودة في هذه المكتبات وتضمينها في ActionDescriptor collection. بدونها، سيرجع السواجر واجهة فارغة لأن لا مسارات مرئية.

نقطة عملية: إن كانت خدمة الـ API تعتمد على مكونات من مكتبات أخرى، تأكد من إضافة `ProjectReference` أو الحزمة المناسبة إلى ملف `.csproj` ثم استدعاء `AddApplicationPart(...)` للتجميعات التي تحتوي Controllers.

---

### ما المقصود بـ Controllers ولماذا أحتاجها؟

- Controller هو كلاس يحتوي على Endpoints (الطرق) التي يتصل بها العميل (الويب، الجوال، واجهة إدارة...).
- في هذا المشروع بعض المكتبات توفر Controllers جاهزة (مثلاً: Profiles API, Orders API, Chats API). هذا يسمح بإعادة استخدام نفس منطق HTTP بدل إعادة كتابته لكل خدمة.
- إذا لم تضف `AddApplicationPart` أو لم تضع Controllers محلياً، فلن يرى التطبيق أي ActionDescriptors وبالتالي القوائم في السواجر ستكون فارغة.

---

### لماذا بدا أننا أنشأنا "خدمة خلفية فارغة"؟

قمتُ بإنشاء هيكل Backend مصغر (Program.cs، DbContext wiring، Seeder) لبدء العمل بسرعة واتباع نفس النمط في المستودع. هذا القالب المقصود به نقطة انطلاق آمنة. لكنه مقصود لأن يكون خفيفاً: لا يحتوي على كل Controllers والمكونات الخارجية تلقائياً حتى تضيف مراجع المكتبات اللازمة وتبيّن لها أن تُحمّل Controllers وتهيئ الخدمات المطلوبة.

الخطوات التالية لملء الخدمة بالفعل:
1. أضف `ProjectReference` للمكتبات التي تحتاجها (Auth, Profiles, Orders, Notifications, Chats, Comments, Catalog, Payments، إلخ).
2. استدعي امتدادات الخدمة الخاصة بكل مكتبة (أسطر `AddACommerce...`) لتهيئة DI والوحدات المصاحبة.
3. أضف `AddApplicationPart(...)` للتجميعات التي تحتوي Controllers حتى تظهر المسارات في السواجر.
4. نفّذ أي Seeder مطلوب لتجهيز بيانات الاختبار.

---

### كيف أستخدم المكتبات المختلفة (نمط الربط والسلسلة المحاسبية للطلب والحجز والرحلة)

فيما يلي لمحة عن المكتبات الشائعة وطريقة ربطها في `Program.cs`، مع ترتيب منطقي لتشغيلها:

1) المصادقة (Authentication)
- ملف المشروع: `libs/backend/integration/ACommerce.Authentication.AspNetCore` وغيرها.
- ما يفعلونه: تسجيل سياسات المصادقة، JWT، واجهات تسجيل الدخول، خدمات المستخدم.
- ضبط في `Program.cs`:

```csharp
builder.Services.AddACommerceAuthentication(builder.Configuration);
// أو: builder.Services.AddACommerceAuthenticationControllers(builder.Configuration);
```

2) ملفات التعريف (Profiles)
- تستخدم لإدارة بيانات المستخدمين، العناوين، نقاط الاتصال.
- طريقة الربط:

```csharp
builder.Services.AddApplicationPart(typeof(ACommerce.Profiles.Api.Controllers.ProfilesController).Assembly);
// إذا وجدت امتداد DI مخصوص للمكتبة: builder.Services.AddACommerceProfiles(builder.Configuration);
```

3) الإشعارات والـ SignalR (Realtime)
- تستخدم لإرسال إشعارات في الوقت الحقيقي للمستخدمين والسائقين.
- نمط الربط:

```csharp
builder.Services.AddACommerceSignalR<NotificationHub, INotificationClient>();
// ثم تأمين endpoint للـ hubs
app.MapHub<NotificationHub>("/hubs/notifications");
```

4) الدردشة (Chats)
```markdown
# How to create a backend API service in this repository (comprehensive)

هذا الدليل الموسع يشرح جميع الخطوات العملية لإنشاء خدمة خلفية (API) باستخدام المكتبات الموجودة في هذا الحل. الهدف: جعل أي خدمة جديدة تتبع نمط المشروع (Shared Kernel, EF helpers, Payments, Transactions/Accounting, Notifications/SignalR) وتلتزم بمبادئ CQRS/MediatR وBaseCrudController حيثما أمكن.

ملاحظة: الشرح بالعربية مع أمثلة أكواد قصيرة بالإنجليزية للحفظ على القابلية للتنفيذ.

---

## موجز سريع (English)
- Create an API project under `Apps/<AppName>/<AppName>.Api` targetting net9.0.
- Add ProjectReferences to shared libraries (SharedKernel, EFCore helpers, Payments, Transactions, Accounting, Notifications, etc.).
- Register domain entities via `EntityDiscoveryRegistry.RegisterEntity<T>();` before registering DbContext.
- Configure DbContext using `AddACommerceSQLite` (dev) or `AddACommerceDbContext` (prod). Use `IRepositoryFactory`/`IBaseAsyncRepository<>` for persistence in services/handlers.
- Prefer MediatR (CQRS): controllers should be thin and delegate to Commands/Queries handlers.
- Include controllers from library assemblies via `AddApplicationPart(...)` so Swagger and MVC discover them.
- Use Transactions.Core + Accounting.Core + Payments.Api for the financial lifecycle (quotes/offers -> order -> invoice -> accounting entries).

---

## 0) Checklist (what you'll do)
- Create API project folder + csproj.
- Add ProjectReference(s) to the libraries your service needs.
- Register Entities in EntityDiscoveryRegistry (before DbContext registration).
- Register DbContext with AddACommerceSQLite / AddACommerceDbContext.
- Register BaseAsyncRepository / RepositoryFactory (hosts usually already register these helpers).
- Register MediatR and add Commands/Queries handlers for core flows.
- Add ApplicationParts for controllers from catalog/payments/transactions packages.
- Add seeders (DocumentType, AttributeDefinitions, sample data).
- Wire Swagger and ensure AddApplicationPart is executed before building the app.

---

## 1) Create the project and csproj

1. Create folder `Apps/<YourApp>/<YourApp>.Api` and a minimal Web API csproj similar to other Apps in the repo.
2. Target `net9.0`, enable `Nullable` and `ImplicitUsings` to match repository conventions.
3. Add ProjectReferences to only the libraries you need. Typical references for a service that uses catalog, payments and transactions:

```xml
<ItemGroup>
  <ProjectReference Include="../../../libs/backend/core/ACommerce.SharedKernel.Abstractions/ACommerce.SharedKernel.Abstractions.csproj" />
  <ProjectReference Include="../../../libs/backend/core/ACommerce.SharedKernel.Infrastructure.EFCores/ACommerce.SharedKernel.Infrastructure.EFCores.csproj" />
  <ProjectReference Include="../../../libs/backend/catalog/ACommerce.Catalog.Products/ACommerce.Catalog.Products.csproj" />
  <ProjectReference Include="../../../libs/backend/other/ACommerce.Transactions.Core/ACommerce.Transactions.Core.csproj" />
  <ProjectReference Include="../../../libs/backend/other/ACommerce.Accounting.Core/ACommerce.Accounting.Core.csproj" />
  <ProjectReference Include="../../../libs/backend/sales/ACommerce.Payments.Api/ACommerce.Payments.Api.csproj" />
</ItemGroup>
```

Only add what you will use; fewer references keeps startup faster.

---

## 2) Register entities (Entity discovery)

Important: register entity types before registering DbContext. The shared ApplicationDbContext scans the EntityDiscoveryRegistry and ACommerce* assemblies when building the model.

In `Program.cs` (early):

```csharp
ACommerce.SharedKernel.Abstractions.Entities.EntityDiscoveryRegistry.RegisterEntity<MyDomainEntity>();
```

If you intend to store ride-specific structured data as attributes, register AttributeDefinition/AttributeOption entities similarly if they live in a separate assembly.

---

## 3) Configure DbContext and DI

Follow these conventions (example `Program.cs` snippet):

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1) Basic infra
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<YourDomainService>();

// 2) Register DbContext (dev: SQLite)
builder.Services.AddACommerceSQLite("Data Source=myservice.db");

// 3) Repositories
builder.Services.AddScoped(typeof(ACommerce.SharedKernel.Abstractions.Repositories.IBaseAsyncRepository<>), typeof(ACommerce.SharedKernel.Infrastructure.EFCore.Repositories.BaseAsyncRepository<>));
builder.Services.AddScoped<ACommerce.SharedKernel.Abstractions.Repositories.IRepositoryFactory, ACommerce.SharedKernel.Infrastructure.EFCore.Factories.RepositoryFactory>();

// 4) MediatR (CQRS)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// 5) Add controllers and application parts (see next section)
builder.Services.AddControllers();

// 6) Add SignalR / Notifications / Payments as needed
builder.Services.AddACommerceSignalR<ACommerce.Notifications.Hubs.NotificationHub, ACommerce.Notifications.Abstractions.Contracts.INotificationClient>();
builder.Services.AddScoped<IPaymentProvider, ACommerce.Payments.Moyasar.Services.MoyasarPaymentProvider>();

var app = builder.Build();

// Ensure DB and seed
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
await db.Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<MySeedService>().SeedAsync();

app.MapControllers();
app.Run();
```

Notes:
- Use `AddACommerceDbContext(options => options.UseSqlServer(...))` for production databases.
- Hosts should register the concrete DbContext once; libraries should not register specific database providers.

---

## 4) Include controllers from library assemblies (AddApplicationPart)

Many common controllers live in library projects. To make them visible to MVC/Swagger you must add their assemblies as application parts.

Call this after `AddControllers()` and before `Build()`:

```csharp
builder.Services.AddControllers()
    .AddApplicationPart(typeof(ACommerce.Catalog.Products.Api.Controllers.ProductsController).Assembly)
    .AddApplicationPart(typeof(ACommerce.Payments.Api.Controllers.PaymentsController).Assembly)
    .AddApplicationPart(typeof(ACommerce.Transactions.Core.Api.Controllers.DocumentOperationsController).Assembly);
```

Then `app.MapControllers()` will expose those endpoints.

---

## 5) Prefer MediatR and BaseCrudController for controllers

- Controllers should be thin: translate HTTP -> Commands/Queries; don't implement business logic in controllers.
- When a resource is straightforward CRUD, inherit the shared `BaseCrudController<TEntity, TCreateDto, TUpdateDto, TResponseDto>` available in many libraries. This reduces boilerplate and matches repository patterns.

Example pattern instead of direct repository use in controller:
```csharp
public class RidesController : ControllerBase
{
  private readonly IMediator _mediator;
  public RidesController(IMediator mediator) => _mediator = mediator;

  [HttpPost("request")]
  public async Task<IActionResult> RequestRide([FromBody] RequestRideDto dto)
  {
    var result = await _mediator.Send(new RequestRideCommand(dto));
    return Ok(result);
  }
}
```

Handler uses IRepositoryFactory/IBaseAsyncRepository<Product> or transactions API to persist.

---

## 6) How to model rides, offers and pricing (recommended)

Follow the domain separation you described (RideRequest vs Offers vs Trip/Order vs Invoice):

- RideRequest: represent as a Document in `Transactions.Core` (DocumentType = "RideRequest") or as a Product with attributes. Documents give you workflow/operations support out of the box.
- Offers (driver bids): represent as `ProductPrice` rows linked to the RideRequest Product, or as `DocumentOperation` entries (Transactions.Core) if you need rich workflow and approvals.
- AcceptedOffer -> create an Order/Booking (use Orders libs if available) and link PaymentId when invoking `Payments.Api`.
- Invoice / Accounting entries: use `ACommerce.Accounting.Core` to create accounting entries (debits/credits) when payment or completion happens.

Concretely: prefer Transactions.Core to model lifecycle events and Accounting.Core to create formal accounting entries. Use Catalog.Products for flexible attributes and ProductPrice entities for price listings.

---

## 7) Seeding required domain metadata

Seed the following at startup (via `MySeedService` called during app startup):
- Document types: `RideRequest`, `DriverOffer`, `TripOrder`, etc. (Transactions.Core API)
- AttributeDefinitions: pick names like `pickup_lat`, `pickup_lng`, `dropoff_lat`, `dropoff_lng`, `scheduled_at`, `rider_id`, `currency`.
- Example ProductCategory / RideCategory if your UI expects them.

Seed example (pseudocode):

```csharp
await seedService.EnsureDocumentTypeAsync("RideRequest", "Ride request from passenger");
await seedService.EnsureAttributeDefinitionAsync("pickup_lat", "Pickup latitude");
// etc.
```

---

## 8) Payments and accounting integration

- On accepted offer, create a PaymentRequest via `Payments.Api` (IPaymentProvider). Store `PaymentId` on the Order/Document.
- On payment webhook/callback, verify payment and then create accounting entries using `Accounting.Core` via its controller/handlers. Use the Transaction/Document correlation ids as references.

Sequence example:
1. Passenger requests ride -> create RideRequest Document
2. Driver offers -> create Offer (ProductPrice or DocumentOperation)
3. Passenger accepts Offer -> create Trip Order Document + request payment
4. Payment success -> create AccountingEntry (Debit: Customer Receivable / Credit: Revenue or Payment provider account) and mark Order as Paid

---

## 9) Dev OTP auth provider (test phone + OTP)

For dev/test flows you can add a simple OTP provider that accepts a fixed test phone and code. Example seeding and behavior:

- Test phone: `771593760`
- Test OTP code: `1234`

Behavior:
1. Client calls `/auth/send-otp` with phone -> server logs or stores code `1234` for the number `771593760` (or returns success for any number in dev mode but only accepts code 1234).
2. Client calls `/auth/verify-otp` with phone + code. If match, server issues a JWT with claims `{sub: userId, phone: phone}`.

Implementation notes:
- Register a dev-only service `IDevOtpProvider` in DI and guard with `if (builder.Environment.IsDevelopment())`.
- Issue JWT using repository's JwtOptions or AddACommerceAuthentication helpers.

---

## 10) Swagger & ApplicationParts checklist

1. Ensure `builder.Services.AddControllers()` is called.
2. Add the required `AddApplicationPart(...)` lines for libraries with controllers.
3. Add `builder.Services.AddACommerceSwagger();` (or other Swagger registration used in repo).
4. Call `app.UseSwagger()` and `app.UseSwaggerUI(...)` before `app.Run()`.
5. If swagger.json fails, check OperationFilters in `libs/backend/integration` for defensive code (TryGetValue), JWT options presence, and that controllers have public actions.

---

## 11) Quality & architecture rules (follow always)

- Controllers must be thin. Use MediatR commands/queries.
- Prefer shared repositories (IRepositoryFactory / IBaseAsyncRepository<>) instead of referencing a concrete DbContext inside domain services.
- Do NOT add a new top-level entity class in a feature library unless the domain cannot be expressed with existing shared models (Product, Document, AccountingEntry...). Follow the reuse-first rule.
- Every backend library must have a corresponding frontend client library (ACommerce.Client.*) or you must add one when adding new backend API surfaces.

---

## 12) Apply this to Rukkab Rider & Driver (quick, actionable steps)

This section maps the above into a minimal, reproducible checklist you can run now to ensure Rider/Driver APIs follow the pattern.

1) Verify ProjectReferences (these are already included in Rukkab apps). If missing, add:

 - `libs/backend/core/ACommerce.SharedKernel.Abstractions`
 - `libs/backend/core/ACommerce.SharedKernel.Infrastructure.EFCores`
 - `libs/backend/other/ACommerce.Transactions.Core`
 - `libs/backend/other/ACommerce.Accounting.Core`
 - `libs/backend/sales/ACommerce.Payments.Api`

2) In `Apps/Rukkab/Rider/Rider.Api/Program.cs` and `Apps/Rukkab/Driver/Driver.Api/Program.cs`:
   - Register entity discovery for any domain entities you add.
   - Ensure the host registers DbContext via `AddACommerceSQLite("Data Source=rukkab.db")` (or separate DB files per host).
   - Add `AddApplicationPart(...)` lines for Payments, Transactions, Accounting, Catalog as shown earlier.
   - Register MediatR and repository factory if not already registered.

3) Seeder: ensure `RukkabSeedDataService` creates DocumentType `RideRequest` and AttributeDefinitions for pickup/dropoff and rider id. Example:

```csharp
await seeder.EnsureDocumentTypeAsync("RideRequest", "Ride request");
await seeder.EnsureAttributeDefinitionAsync("pickup_lat", "Pickup latitude");
await seeder.EnsureAttributeDefinitionAsync("pickup_lng", "Pickup longitude");
await seeder.EnsureAttributeDefinitionAsync("rider_id", "Rider identifier");
```

4) Orchestrator: refactor `PersistentRideOrchestrator` to use `IRepositoryFactory` and Transactions.Core APIs (we started this refactor). Ensure `RequestRideCommand` exists and its Handler creates the Document + publishes domain events.

5) OTP auth: add dev OTP provider (accept 771593760/1234) and expose `/auth/send-otp` and `/auth/verify-otp` endpoints or use existing Auth libraries with a dev-mode provider.

6) Run and verify:

```bash
./scripts/run-rukkab.sh
curl -v http://127.0.0.1:5001/swagger/v1/swagger.json  # Rider
curl -v http://127.0.0.1:5002/swagger/v1/swagger.json  # Driver
```

If swagger lists endpoints, the ApplicationParts and DI wiring are correct.

---

## 13) Example tasks I can do for you now (pick one)
- Expand `RukkabSeedDataService` to seed DocumentType + AttributeDefinitions (I can implement this).  — RECOMMENDED NEXT STEP
- Update `PersistentRideOrchestrator` to persist ride metadata in Transactions.Core + ProductPrice/ProductAttribute (I already refactored to Product earlier; I can continue to move to Transactions.Core).
- Add a dev OTP provider and wire authentication endpoints (771593760 / 1234).
- Add `AddApplicationPart(...)` lines to Rider and Driver `Program.cs` and enable Swagger UI if missing.

---

إذا رغبت، أبدأ فوراً بتطبيق أحد البنود أعلاه وأجري اختبار تشغيل سريع محلياً. أختر أي مهمة تريدني أن أنفذ الآن (أقترح: seed DocumentType & attributes ثم تمهيد MediatR handler لـ RequestRide). 

---

## مثال عملي: خدمة Rider و Driver بالحد الأدنى (minimal, runnable sketch)

الهدف: أمثلة بسيطة قابلة للنسخ تُظهر كيف تُعرّف Service Host وControllers وHandlers لنطاق الرحلة من "البحث" حتى "التقييم". سنركز على نقاط النهاية التي يستهلكها ملف فحص الرحلة (search -> request -> offers -> accept -> arrived -> start -> complete -> rate).

ملاحظة مهمة: لا نضيف أي تكامل للدفع الآن — التطبيق سيعمل في بيئة اليمن حيث لا يتوفر الدفع الإلكتروني عادةً. لذا قسم Rider لن يتضمن أي استدعاء أو متطلبات لـ Payments.Api أو مقدّم دفع. ضع هذا في الحسبان عند تصميم واجهات المستخدم وواجهات الاختبار.

ملاحظة هندسية: الأمثلة تُظهر استخدام MediatR و`IRepositoryFactory`/`IBaseAsyncRepository<>` والاعتماد على `Product`/`ProductPrice`/`ProductAttribute` أو Transactions.Core حسب الحاجة. اخترت عرض نهج مبسّط قابل للتطبيق بسرعة.

### 1) Minimal `Program.cs` (Rider)

ضع ملف `Apps/Rukkab/Rider/Rider.Api/Program.cs` مشابهاً للآتي (مقتطف مختصر):

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register entity discovery early if you add entities (example shown elsewhere)

// DB and shared infra
builder.Services.AddACommerceSQLite("Data Source=rukkab-rider.db");
builder.Services.AddScoped(typeof(ACommerce.SharedKernel.Abstractions.Repositories.IBaseAsyncRepository<>), typeof(ACommerce.SharedKernel.Infrastructure.EFCore.Repositories.BaseAsyncRepository<>));
builder.Services.AddScoped<ACommerce.SharedKernel.Abstractions.Repositories.IRepositoryFactory, ACommerce.SharedKernel.Infrastructure.EFCore.Factories.RepositoryFactory>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Add controllers and include application parts for reused controllers (catalog, transactions)
builder.Services.AddControllers()
  .AddApplicationPart(typeof(ACommerce.Catalog.Products.Api.Controllers.ProductsController).Assembly)
  .AddApplicationPart(typeof(ACommerce.Transactions.Core.Api.Controllers.DocumentOperationsController).Assembly);

// Dev OTP provider (register only in development)
// builder.Services.AddSingleton<IDevOtpProvider, DevOtpProvider>();

builder.Services.AddACommerceSwagger();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
await db.Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<MySeedService>().SeedAsync();

app.MapControllers();
app.Run();
```

### 2) Minimal `Program.cs` (Driver)

`Apps/Rukkab/Driver/Driver.Api/Program.cs` مشابه مع قاعدة بيانات منفصلة وخدمات مطابقة:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddACommerceSQLite("Data Source=rukkab-driver.db");
builder.Services.AddScoped(typeof(ACommerce.SharedKernel.Abstractions.Repositories.IBaseAsyncRepository<>), typeof(ACommerce.SharedKernel.Infrastructure.EFCore.Repositories.BaseAsyncRepository<>));
builder.Services.AddScoped<ACommerce.SharedKernel.Abstractions.Repositories.IRepositoryFactory, ACommerce.SharedKernel.Infrastructure.EFCore.Factories.RepositoryFactory>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddControllers()
  .AddApplicationPart(typeof(ACommerce.Transactions.Core.Api.Controllers.DocumentOperationsController).Assembly);
builder.Services.AddACommerceSwagger();
var app = builder.Build();
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
await db.Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<MySeedService>().SeedAsync();
app.MapControllers();
app.Run();
```

### 3) Endpoints (Rider) — minimal controller sketch

ضع `Apps/Rukkab/Rider/Rider.Api/Controllers/RidesController.cs` مع واجهات بسيطة التي يستدعيها ملف الاختبار:

```csharp
[ApiController]
[Route("api/rides")]
public class RidesController : ControllerBase
{
  private readonly IMediator _mediator;
  public RidesController(IMediator mediator) => _mediator = mediator;

  [HttpGet("drivers/search")]
  public Task<IActionResult> SearchDrivers([FromQuery] double lat, [FromQuery] double lng)
    => _mediator.Send(new SearchDriversQuery(lat, lng)).ContinueWith(t => (IActionResult)Ok(t.Result));

  [HttpPost("request")]
  public async Task<IActionResult> RequestRide([FromBody] RequestRideDto dto)
  {
    var result = await _mediator.Send(new RequestRideCommand(dto));
    return Ok(result); // returns ride id or created Product/Document summary
  }

  [HttpGet("{rideId}")]
  public async Task<IActionResult> GetRide(Guid rideId) => Ok(await _mediator.Send(new GetRideQuery(rideId)));

  [HttpPost("{rideId}/cancel")]
  public async Task<IActionResult> CancelRide(Guid rideId) => Ok(await _mediator.Send(new CancelRideCommand(rideId)));

  [HttpPost("{rideId}/rate")]
  public async Task<IActionResult> RateRide(Guid rideId, [FromBody] RateRideDto dto) => Ok(await _mediator.Send(new RateRideCommand(rideId, dto)));
}
```

Notes:
- `SearchDriversQuery` returns a list of nearby driver DTOs (id, name, vehicle, distance).
- `RequestRideCommand` creates a Product or a Transactions.Core Document for the ride request (seed DocumentType `RideRequest` and attribute definitions first).

### 4) Endpoints (Driver) — minimal controller sketch

`Apps/Rukkab/Driver/Driver.Api/Controllers/DriverTasksController.cs`:

```csharp
[ApiController]
[Route("api/driver")]
public class DriverTasksController : ControllerBase
{
  private readonly IMediator _mediator;
  public DriverTasksController(IMediator mediator) => _mediator = mediator;

  [HttpGet("offers/pending")]
  public Task<IActionResult> PendingOffers() => _mediator.Send(new GetPendingOffersQuery()).ContinueWith(t => (IActionResult)Ok(t.Result));

  [HttpPost("offers/{rideId}/accept")]
  public async Task<IActionResult> AcceptOffer(Guid rideId, [FromBody] AcceptOfferDto dto) => Ok(await _mediator.Send(new AcceptOfferCommand(rideId, dto.DriverId, dto.Price)));

  [HttpPost("rides/{rideId}/arrived")]
  public async Task<IActionResult> MarkArrived(Guid rideId) => Ok(await _mediator.Send(new DriverArrivedCommand(rideId)));

  [HttpPost("rides/{rideId}/start")]
  public async Task<IActionResult> StartRide(Guid rideId) => Ok(await _mediator.Send(new StartRideCommand(rideId)));

  [HttpPost("rides/{rideId}/complete")]
  public async Task<IActionResult> CompleteRide(Guid rideId) => Ok(await _mediator.Send(new CompleteRideCommand(rideId)));
}
```

### 5) DTOs and Commands (minimal)

Create shared DTOs under `Apps/Rukkab/Shared/` or as small classes inside the Api projects used by MediatR handlers.

Examples (pseudocode):

```csharp
public record RequestRideDto(double PickupLat, double PickupLng, double DropoffLat, double DropoffLng, string RiderId);
public record RateRideDto(int Stars, string? Comment);
public record AcceptOfferDto(string DriverId, decimal Price);

public record RequestRideCommand(RequestRideDto Dto) : IRequest<Guid>;
public record GetRideQuery(Guid RideId) : IRequest<RideSummaryDto>;
// etc.
```

Handlers should use `IRepositoryFactory.CreateRepository<Product>()` or the Transactions.Core client to persist ride requests as Products/Document operations.

### 6) Simple persistence guidance (no bespoke Ride class)

- Per your rule: avoid creating a new Ride entity unless necessary. Use `Product` + `ProductAttribute` / `ProductPrice` or Transactions.Core Documents.
- Minimal approach: create a `Product` with a unique Id (the ride id), store serialized details in `ShortDescription` temporarily, but seed attribute definitions and migrate important fields to `ProductAttribute` and pricing offers to `ProductPrice` soon after.

Example handler steps for RequestRideCommand (pseudocode):
1. repo = repoFactory.CreateRepository<Product>();
2. create product p = new Product { Name = "RideRequest", Barcode = dto.RiderId, ShortDescription = JsonSerializer.Serialize(payload) };
3. await repo.AddAsync(p);
4. return p.Id;

### 7) No payments in Rider for now (Yemen environment)

تذكير مهم: لا تضف أي واجهات أو نماذج لدفع إلكتروني في خدمة الركاب الآن. في بيئة التشغيل (اليمن) لا يتوفر الدفع الإلكتروني في هذا المشروع. عند الحاجة لاحقاً، سنضيف `Payments.Api` وندمجها بعد دراسة طرق الدفع المحلية (تحويل بنكي، دفع عند الاستلام، إلخ).

### 8) Testing the flow (smoke test)

1) شغّل الخدمات:

```bash
./scripts/run-rukkab.sh
```

2) اختبار تسلسل الطلب البسيط عبر curl or HTTP client (مثال مبسط):

```bash
# search drivers
curl -s "http://127.0.0.1:5001/api/rides/drivers/search?lat=15.355&lng=44.208" | jq .

# request ride
curl -s -X POST http://127.0.0.1:5001/api/rides/request -H "Content-Type: application/json" -d '{"PickupLat":15.355,"PickupLng":44.208,"DropoffLat":15.346,"DropoffLng":44.220,"RiderId":"rider-1"}' | jq .

# driver lists offers and accepts (driver service)
curl -s http://127.0.0.1:5002/api/driver/offers/pending | jq .
curl -s -X POST http://127.0.0.1:5002/api/driver/offers/<rideId>/accept -H "Content-Type: application/json" -d '{"DriverId":"driver-1","Price":5.00}'

# mark arrived, start, complete from driver
curl -s -X POST http://127.0.0.1:5002/api/driver/rides/<rideId>/arrived
curl -s -X POST http://127.0.0.1:5002/api/driver/rides/<rideId>/start
curl -s -X POST http://127.0.0.1:5002/api/driver/rides/<rideId>/complete

# rider posts rating
curl -s -X POST http://127.0.0.1:5001/api/rides/<rideId>/rate -H "Content-Type: application/json" -d '{"Stars":5,"Comment":"Great ride"}'
```

هذه الخطوات تغطي مسار الاختبار من البحث حتى التقييم دون إدخال الدفع. عند الحاجة لإضافة الدفع، سنحدّث الراوتات وHandlers لربط Payments.Api واستقبال webhooks.

---

أكملت إضافة مثال الحد الأدنى للخدمتين Rider وDriver في الدليل. أخبرني إذا تريدني أن:
- أنشئ فعلياً ملفات الكود (Program.cs, Controllers, DTOs, Handlers) في المسارات `Apps/Rukkab/...` وأجري build/run وSmoke test محلياً.
- أدمج Seeders لمواصفات DocumentType وAttributeDefinitions المطلوبة الآن.

