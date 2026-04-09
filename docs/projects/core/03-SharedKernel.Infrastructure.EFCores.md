# ACommerce.SharedKernel.Infrastructure.EFCores

## نظرة عامة
تنفيذ البنية التحتية لـ Entity Framework Core مع دعم Auto-Discovery للكيانات، البحث الذكي، الفلاتر المتقدمة، والحذف المنطقي. توفر DbContext موحد ومستودع جاهز للاستخدام.

## الموقع
`/Core/ACommerce.SharedKernel.Infrastructure.EFCores`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`
- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.InMemory`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Microsoft.EntityFrameworkCore.Sqlite`

---

## ApplicationDbContext

### وصف
DbContext موحد مع اكتشاف تلقائي لجميع الكيانات من مكتبات ACommerce.

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
```

### الميزات
1. **Auto-Discovery**: يكتشف تلقائياً جميع الـ Types التي تطبق `IBaseEntity`
2. **تحميل من جميع المكتبات**: يبحث في جميع Assemblies التي تبدأ بـ `ACommerce`
3. **تطبيق Configurations**: يطبق تلقائياً `IEntityTypeConfiguration` من جميع المكتبات

### كيفية الاكتشاف
```csharp
var assemblies = AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.FullName?.StartsWith("ACommerce") == true);

// يبحث عن:
// - Classes تطبق IBaseEntity
// - غير Abstract
// - غير Generic
// - غير Nested
```

---

## BaseAsyncRepository

### وصف
المستودع الأساسي الذي ينفذ `IBaseAsyncRepository<T>` مع دعم كامل لـ EF Core.

```csharp
public class BaseAsyncRepository<T> : IBaseAsyncRepository<T>
    where T : class, IBaseEntity
```

### القراءة الأساسية

#### GetByIdAsync
```csharp
// جلب بالمعرف (بدون المحذوفات)
var entity = await repository.GetByIdAsync(id);

// جلب بالمعرف (مع المحذوفات)
var entity = await repository.GetByIdAsync(id, includeDeleted: true);
```

#### ListAllAsync
```csharp
// جلب الكل (بدون المحذوفات)
var entities = await repository.ListAllAsync();

// جلب الكل (مع المحذوفات)
var entities = await repository.ListAllAsync(includeDeleted: true);
```

### البحث والتصفية المتقدمة

#### GetAllWithPredicateAsync
```csharp
var products = await repository.GetAllWithPredicateAsync(
    predicate: p => p.Price > 100,
    includeDeleted: false,
    includeProperties: "Category", "Brand"
);
```

#### GetPagedAsync
```csharp
var result = await repository.GetPagedAsync(
    pageNumber: 1,
    pageSize: 20,
    predicate: p => p.IsActive,
    orderBy: p => p.CreatedAt,
    ascending: false,
    includeDeleted: false,
    includeProperties: "Category"
);

// result.Items, result.TotalCount, result.PageNumber, result.PageSize
```

#### SmartSearchAsync
البحث الذكي مع الفلاتر والصفحات:
```csharp
var result = await repository.SmartSearchAsync(new SmartSearchRequest
{
    SearchTerm = "هاتف سامسونج",  // يبحث في جميع الخصائص النصية
    PageNumber = 1,
    PageSize = 20,
    OrderBy = "Price",
    Ascending = true,
    IncludeDeleted = false,
    IncludeProperties = new List<string> { "Category", "Brand" },
    Filters = new List<FilterItem>
    {
        new() { PropertyName = "Price", Operator = FilterOperator.LessThan, Value = 5000 },
        new() { PropertyName = "IsActive", Operator = FilterOperator.Equals, Value = true }
    }
});
```

### البحث النصي الذكي
`ApplySmartTextSearch` يبحث تلقائياً في **جميع الخصائص النصية**:
- يستخدم `ToLower()` للمقارنة (case-insensitive)
- يتجاهل القيم `null`
- يدمج الشروط بـ `OR` (أي خاصية تحتوي على النص)

### الفلاتر المدعومة

| FilterOperator | الوصف | مثال |
|----------------|-------|------|
| `Equals` | يساوي | `Price = 100` |
| `NotEquals` | لا يساوي | `Status != "Deleted"` |
| `Contains` | يحتوي (string) | `Name.Contains("phone")` |
| `StartsWith` | يبدأ بـ (string) | `Name.StartsWith("Sam")` |
| `EndsWith` | ينتهي بـ (string) | `Email.EndsWith("@gmail.com")` |
| `GreaterThan` | أكبر من | `Price > 100` |
| `LessThan` | أصغر من | `Price < 1000` |
| `GreaterThanOrEqual` | أكبر من أو يساوي | `Price >= 100` |
| `LessThanOrEqual` | أصغر من أو يساوي | `Price <= 1000` |
| `Between` | بين قيمتين | `Price >= 100 AND Price <= 500` |
| `IsNull` | فارغ | `Description IS NULL` |
| `IsNotNull` | غير فارغ | `Description IS NOT NULL` |

### الإضافة

#### AddAsync
```csharp
var product = new Product { Name = "منتج جديد", Price = 99.99m };
var created = await repository.AddAsync(product);
// يضع تلقائياً: Id (Guid), CreatedAt (UTC), IsDeleted = false
```

#### AddRangeAsync
```csharp
var products = new List<Product> { product1, product2, product3 };
var created = await repository.AddRangeAsync(products);
```

### التحديث

#### UpdateAsync
```csharp
product.Name = "اسم جديد";
await repository.UpdateAsync(product);
// يضع تلقائياً: UpdatedAt = DateTime.UtcNow
// يتجاهل: Id, CreatedAt, IsDeleted
```

#### PartialUpdateAsync
```csharp
await repository.PartialUpdateAsync(productId, new Dictionary<string, object>
{
    ["Name"] = "اسم جديد",
    ["Price"] = 149.99m
});
// يحدث الحقول المحددة فقط
// يحمي: Id, CreatedAt, IsDeleted
```

### الحذف

#### Hard Delete
```csharp
await repository.DeleteAsync(entity);
// أو
await repository.DeleteAsync(entityId);
// حذف نهائي من قاعدة البيانات
```

#### Soft Delete
```csharp
await repository.SoftDeleteAsync(entity);
// أو
await repository.SoftDeleteAsync(entityId);
// يضع: IsDeleted = true, UpdatedAt = now
```

#### RestoreAsync
```csharp
await repository.RestoreAsync(entityId);
// يضع: IsDeleted = false, UpdatedAt = now
```

#### DeleteRangeAsync
```csharp
// حذف منطقي لمجموعة
await repository.DeleteRangeAsync(entities, softDelete: true);

// حذف نهائي لمجموعة
await repository.DeleteRangeAsync(entities, softDelete: false);
```

### الإحصائيات

#### CountAsync
```csharp
var totalCount = await repository.CountAsync();
var activeCount = await repository.CountAsync(p => p.IsActive);
var allIncludingDeleted = await repository.CountAsync(includeDeleted: true);
```

#### ExistsAsync
```csharp
var exists = await repository.ExistsAsync(p => p.Email == "test@example.com");
```

---

## RepositoryFactory

### وصف
مصنع لإنشاء مستودعات ديناميكياً.

```csharp
public class RepositoryFactory(IServiceProvider serviceProvider) : IRepositoryFactory
{
    public IBaseAsyncRepository<T> CreateRepository<T>() where T : class, IBaseEntity
    {
        var dbContext = serviceProvider.GetRequiredService<DbContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<BaseAsyncRepository<T>>>();
        return new BaseAsyncRepository<T>(dbContext, logger);
    }
}
```

### الاستخدام
```csharp
public class MyService
{
    private readonly IRepositoryFactory _factory;

    public async Task DoWork()
    {
        var productRepo = _factory.CreateRepository<Product>();
        var categoryRepo = _factory.CreateRepository<Category>();

        // استخدم المستودعات...
    }
}
```

---

## ServiceCollectionExtensions

### تسجيل الخدمات

#### AddACommerceDbContext (الأساسية)
```csharp
services.AddACommerceDbContext(options =>
    options.UseSqlServer(connectionString));
```

#### AddACommerceSqlServer
```csharp
services.AddACommerceSqlServer(connectionString);
```

#### AddACommercePostgreSQL
```csharp
services.AddACommercePostgreSQL(connectionString);
```

#### AddACommerceSQLite
```csharp
services.AddACommerceSQLite(connectionString);
```

#### AddACommerceInMemoryDatabase (للتجربة)
```csharp
services.AddACommerceInMemoryDatabase("TestDb");
```

### ما يتم تسجيله
1. `ApplicationDbContext` كـ DbContext
2. `DbContext` للتوافق مع الكود القديم
3. `IRepositoryFactory` → `RepositoryFactory`

---

## ModelBuilderExtensions

### ApplyBaseEntityConfiguration
يطبق تكوينات مشتركة لجميع الكيانات:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyBaseEntityConfiguration();
}
```

**ما يفعله:**
- Index على `CreatedAt` باسم `IX_{EntityName}_CreatedAt`
- Index على `IsDeleted` باسم `IX_{EntityName}_IsDeleted`
- Global Query Filter لـ Soft Delete (`WHERE IsDeleted = false`)

### ApplyBaseEntityColumnConfiguration
يضبط أعمدة IBaseEntity:

```csharp
modelBuilder.ApplyBaseEntityColumnConfiguration();
```

**ما يفعله:**
- `Id` كـ Primary Key
- `CreatedAt` Required مع قيمة افتراضية `GETUTCDATE()`
- `UpdatedAt` Nullable
- `IsDeleted` Required مع قيمة افتراضية `false`

---

## بنية الملفات
```
ACommerce.SharedKernel.Infrastructure.EFCores/
├── Context/
│   └── ApplicationDbContext.cs
├── Repositories/
│   └── BaseAsyncRepository.cs
├── Factories/
│   └── RepositoryFactory.cs
└── Extensions/
    ├── ServiceCollectionExtensions.cs
    └── ModelBuilderExtensions.cs
```

---

## مثال استخدام كامل

### Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);

// تسجيل قاعدة البيانات
builder.Services.AddACommerceSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")!);

// تسجيل CQRS
builder.Services.AddSharedKernelCQRS();

var app = builder.Build();
```

### استخدام في Controller
```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductResponseDto>>> Search(
        [FromQuery] SmartSearchRequest request)
    {
        var query = new SmartSearchQuery<Product, ProductResponseDto>
        {
            Request = request
        };

        return await _mediator.Send(query);
    }
}
```

---

## ملاحظات تقنية

1. **Expression Trees**: يستخدم Expression Trees لبناء الاستعلامات ديناميكياً
2. **Concurrency Handling**: يلتقط `DbUpdateConcurrencyException` ويسجلها
3. **Logging**: جميع العمليات مسجلة في Log مع مستويات مناسبة
4. **Protected Fields**: لا يمكن تعديل `Id`, `CreatedAt`, `IsDeleted` عبر Update
5. **Auto Timestamps**: يضع تلقائياً `CreatedAt` و `UpdatedAt`
6. **Global Query Filter**: يمكن تجاوزه عبر `IgnoreQueryFilters()` في EF Core
