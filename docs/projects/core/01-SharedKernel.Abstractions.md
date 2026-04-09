# ACommerce.SharedKernel.Abstractions

## معلومات المشروع | Project Info

| الخاصية | القيمة |
|---------|--------|
| **المسار** | `Core/ACommerce.SharedKernel.Abstractions` |
| **النوع** | Class Library |
| **الإطار** | .NET 9.0 |
| **الاعتماديات** | لا توجد (Zero Dependencies) |

## الوصف | Description

المكتبة الأساسية التي تحتوي على جميع الواجهات والعقود الأساسية لنظام ACommerce. هذه المكتبة لا تعتمد على أي مكتبات أخرى وتشكل الأساس الذي تبني عليه جميع المكتبات الأخرى.

---

## الهيكل | Structure

```
ACommerce.SharedKernel.Abstractions/
├── Entities/
│   ├── IBaseEntity.cs
│   └── IDomainEvent.cs
├── Repositories/
│   ├── IBaseAsyncRepository.cs
│   └── IRepositoryFactory.cs
├── Queries/
│   ├── SmartSearchRequest.cs
│   ├── PagedResult.cs
│   ├── FilterItem.cs
│   └── FilterOperator.cs
├── Results/
│   └── OperationResult.cs
└── Exceptions/
    └── DomainException.cs
```

---

## المكونات | Components

### 1. IBaseEntity

الواجهة الأساسية لجميع الكيانات في النظام.

```csharp
public interface IBaseEntity
{
    Guid Id { get; set; }           // المعرف الفريد
    DateTime CreatedAt { get; set; } // تاريخ الإنشاء (UTC)
    DateTime? UpdatedAt { get; set; } // تاريخ آخر تحديث (UTC)
    bool IsDeleted { get; set; }     // علامة الحذف المنطقي
}
```

**الاستخدام:**
```csharp
public class Product : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // خصائص المنتج
    public string Name { get; set; }
    public decimal Price { get; set; }
}
```

---

### 2. IDomainEvent

Marker interface للأحداث النطاقية.

```csharp
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; init; }
}
```

**الاستخدام:**
```csharp
public record OrderCreatedEvent(
    Guid OrderId,
    string OrderNumber,
    decimal Total
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

---

### 3. IBaseAsyncRepository<T>

المستودع الأساسي مع جميع عمليات CRUD والبحث المتقدم.

#### عمليات القراءة:
```csharp
// الحصول على كيان بالمعرف
Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
Task<T?> GetByIdAsync(Guid id, bool includeDeleted, CancellationToken ct = default);

// الحصول على جميع الكيانات
Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default);
Task<IReadOnlyList<T>> ListAllAsync(bool includeDeleted, CancellationToken ct = default);
```

#### البحث والتصفية:
```csharp
// البحث بشرط
Task<IReadOnlyList<T>> GetAllWithPredicateAsync(
    Expression<Func<T, bool>>? predicate = null,
    bool includeDeleted = false,
    params string[] includeProperties);

// التصفح
Task<PagedResult<T>> GetPagedAsync(
    int pageNumber = 1,
    int pageSize = 10,
    Expression<Func<T, bool>>? predicate = null,
    Expression<Func<T, object>>? orderBy = null,
    bool ascending = true,
    bool includeDeleted = false,
    params string[] includeProperties);

// البحث الذكي
Task<PagedResult<T>> SmartSearchAsync(
    SmartSearchRequest request,
    CancellationToken ct = default);
```

#### عمليات الكتابة:
```csharp
// الإضافة
Task<T> AddAsync(T entity, CancellationToken ct = default);
Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

// التحديث
Task UpdateAsync(T entity, CancellationToken ct = default);
Task PartialUpdateAsync(Guid id, Dictionary<string, object> updates, CancellationToken ct = default);
```

#### عمليات الحذف:
```csharp
// الحذف النهائي (Hard Delete)
Task DeleteAsync(T entity, CancellationToken ct = default);
Task DeleteAsync(Guid id, CancellationToken ct = default);

// الحذف المنطقي (Soft Delete)
Task SoftDeleteAsync(T entity, CancellationToken ct = default);
Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

// الاستعادة
Task RestoreAsync(Guid id, CancellationToken ct = default);

// حذف مجموعة
Task DeleteRangeAsync(IEnumerable<T> entities, bool softDelete = true, CancellationToken ct = default);
```

#### الإحصائيات:
```csharp
Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false, CancellationToken ct = default);
Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false, CancellationToken ct = default);
```

---

### 4. SmartSearchRequest

طلب البحث الذكي مع التصفية والترتيب والتصفح.

```csharp
public class SmartSearchRequest
{
    public string? SearchTerm { get; set; }         // مصطلح البحث
    public List<FilterItem>? Filters { get; set; } // الفلاتر
    public int PageNumber { get; set; } = 1;        // رقم الصفحة
    public int PageSize { get; set; } = 10;         // حجم الصفحة
    public string? OrderBy { get; set; }            // الترتيب
    public bool Ascending { get; set; } = true;     // تصاعدي/تنازلي
    public List<string>? IncludeProperties { get; set; } // Navigation Properties
    public bool IncludeDeleted { get; set; } = false;    // تضمين المحذوفات

    public bool IsValid(); // التحقق من الصحة
}
```

---

### 5. FilterItem و FilterOperator

نظام تصفية مرن ومتقدم.

```csharp
public class FilterItem
{
    public required string PropertyName { get; set; }  // اسم الخاصية
    public object? Value { get; set; }                 // القيمة
    public object? SecondValue { get; set; }           // القيمة الثانية (للـ Between)
    public FilterOperator Operator { get; set; } = FilterOperator.Equals;
}

public enum FilterOperator
{
    Equals,              // المساواة
    NotEquals,           // عدم المساواة
    Contains,            // يحتوي على
    StartsWith,          // يبدأ بـ
    EndsWith,            // ينتهي بـ
    GreaterThan,         // أكبر من
    LessThan,            // أقل من
    GreaterThanOrEqual,  // أكبر من أو يساوي
    LessThanOrEqual,     // أقل من أو يساوي
    Between,             // بين قيمتين
    In,                  // موجود في قائمة
    NotIn,               // غير موجود في قائمة
    IsNull,              // قيمة فارغة
    IsNotNull            // قيمة غير فارغة
}
```

**مثال استخدام:**
```csharp
var request = new SmartSearchRequest
{
    SearchTerm = "هاتف",
    Filters = new List<FilterItem>
    {
        new() { PropertyName = "Price", Value = 100, SecondValue = 500, Operator = FilterOperator.Between },
        new() { PropertyName = "CategoryId", Value = categoryId, Operator = FilterOperator.Equals },
        new() { PropertyName = "Status", Value = new[] { "Active", "Featured" }, Operator = FilterOperator.In }
    },
    PageNumber = 1,
    PageSize = 20,
    OrderBy = "CreatedAt",
    Ascending = false
};

var result = await _repository.SmartSearchAsync(request);
```

---

### 6. PagedResult<T>

نتيجة مقسمة إلى صفحات مع معلومات التنقل.

```csharp
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; }       // العناصر
    public int TotalCount { get; set; }               // العدد الكلي
    public int PageNumber { get; set; }               // رقم الصفحة
    public int PageSize { get; set; }                 // حجم الصفحة

    // خصائص محسوبة
    public int TotalPages { get; }                    // عدد الصفحات
    public bool HasNextPage { get; }                  // هل يوجد صفحة تالية
    public bool HasPreviousPage { get; }              // هل يوجد صفحة سابقة
    public int? NextPageNumber { get; }               // رقم الصفحة التالية
    public int? PreviousPageNumber { get; }           // رقم الصفحة السابقة

    public Dictionary<string, object>? Metadata { get; set; } // معلومات إضافية

    // Factory Methods
    public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 10);
    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize);
}
```

---

### 7. OperationResult<T>

نمط النتيجة للتعامل مع النجاح والفشل.

```csharp
public class OperationResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    // Factory Methods
    public static OperationResult<T> SuccessResult(T data);
    public static OperationResult<T> FailureResult(string errorMessage, string? errorCode = null);
    public static OperationResult<T> ValidationFailure(List<string> errors);
    public static OperationResult<T> ValidationFailure(Dictionary<string, string> errors);
}

// نسخة بدون بيانات
public class OperationResult
{
    // نفس الخصائص والـ Factory Methods
}
```

**مثال:**
```csharp
public async Task<OperationResult<Guid>> CreateProductAsync(CreateProductDto dto)
{
    if (string.IsNullOrEmpty(dto.Name))
        return OperationResult<Guid>.ValidationFailure(new() { ["Name"] = "الاسم مطلوب" });

    var product = new Product { /* ... */ };
    await _repository.AddAsync(product);

    return OperationResult<Guid>.SuccessResult(product.Id);
}
```

---

### 8. الاستثناءات | Exceptions

```csharp
// استثناء عام للنطاق
public class DomainException : Exception
{
    public string ErrorCode { get; }
}

// عدم العثور على الكيان
public class EntityNotFoundException : DomainException
{
    // "ENTITY_NOT_FOUND"
}

// خطأ التحقق من الصحة
public class ValidationException : DomainException
{
    public Dictionary<string, string> Errors { get; }
    // "VALIDATION_ERROR"
}

// عدم الصلاحية
public class UnauthorizedException : DomainException
{
    // "UNAUTHORIZED"
}

// تعارض العمليات المتزامنة
public class ConcurrencyException : DomainException
{
    // "CONCURRENCY_ERROR"
}
```

---

### 9. IRepositoryFactory

مصنع المستودعات.

```csharp
public interface IRepositoryFactory
{
    IBaseAsyncRepository<T> CreateRepository<T>() where T : class, IBaseEntity;
}
```

---

## أنماط التصميم المستخدمة | Design Patterns

| النمط | الاستخدام |
|-------|----------|
| **Repository Pattern** | `IBaseAsyncRepository<T>` |
| **Factory Pattern** | `IRepositoryFactory` |
| **Result Pattern** | `OperationResult<T>` |
| **Marker Interface** | `IDomainEvent` |

---

## المشاريع المعتمدة على هذه المكتبة | Dependent Projects

جميع مشاريع ACommerce تعتمد على هذه المكتبة، بما في ذلك:
- `ACommerce.SharedKernel.CQRS`
- `ACommerce.SharedKernel.Infrastructure.EFCores`
- `ACommerce.Catalog.*`
- `ACommerce.Orders`
- وجميع المكتبات الأخرى
