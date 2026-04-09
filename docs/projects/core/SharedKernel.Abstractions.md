# ACommerce.SharedKernel.Abstractions

## نظرة عامة | Overview

مكتبة `ACommerce.SharedKernel.Abstractions` هي الأساس المعماري لنظام ACommerce بأكمله. تحتوي على جميع الواجهات الأساسية (Interfaces) والعقود (Contracts) والأنماط المجردة (Abstract Patterns) التي تبني عليها جميع المكتبات الأخرى.

This library is the architectural foundation of the entire ACommerce system. It contains all base interfaces, contracts, and abstract patterns that all other libraries build upon.

**المسار | Path:** `SharedKernel/ACommerce.SharedKernel.Abstractions`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)
**الاعتماديات | Dependencies:** None (Zero Dependencies)

---

## المكونات الرئيسية | Core Components

### 1. الكيانات الأساسية | Base Entities

#### IEntity<TId>
الواجهة الأساسية لجميع الكيانات في النظام.

```csharp
public interface IEntity<TId>
{
    TId Id { get; set; }
}
```

**الاستخدام | Usage:**
- جميع كيانات قاعدة البيانات يجب أن تنفذ هذه الواجهة
- `TId` يمكن أن يكون `Guid`, `int`, `long`, أو `string`

#### IAuditableEntity
واجهة للكيانات التي تحتاج تتبع تاريخ الإنشاء والتعديل.

```csharp
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    string? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
}
```

**الاستخدام | Usage:**
- تتبع من أنشأ الكيان ومتى
- تتبع آخر تعديل ومن قام به
- مفيد للتدقيق والمراجعة

#### ISoftDeletable
واجهة للحذف الناعم (Soft Delete) بدلاً من الحذف الفعلي.

```csharp
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}
```

**الاستخدام | Usage:**
- الكيانات المحذوفة تبقى في قاعدة البيانات
- يمكن استرجاع البيانات المحذوفة
- الاستعلامات تستبعد المحذوف تلقائياً

#### IMultiTenantEntity
واجهة لدعم تعدد المستأجرين (Multi-Tenancy).

```csharp
public interface IMultiTenantEntity
{
    Guid TenantId { get; set; }
}
```

**الاستخدام | Usage:**
- كل كيان ينتمي لمستأجر معين
- عزل البيانات بين المستأجرين تلقائياً
- أساس نموذج SaaS

---

### 2. نمط المستودع | Repository Pattern

#### IRepository<TEntity, TId>
الواجهة الأساسية للمستودعات مع عمليات CRUD الكاملة.

```csharp
public interface IRepository<TEntity, TId> where TEntity : class, IEntity<TId>
{
    // Query Operations
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    // Command Operations
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TId id, CancellationToken cancellationToken = default);

    // Existence Check
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    // Count
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);
}
```

**الميزات | Features:**
- عمليات CRUD الكاملة
- دعم الاستعلامات المرنة عبر `Expression`
- دعم `CancellationToken` لجميع العمليات
- دعم العمليات المجمعة

#### IReadOnlyRepository<TEntity, TId>
مستودع للقراءة فقط - مفيد لأنماط CQRS.

```csharp
public interface IReadOnlyRepository<TEntity, TId> where TEntity : class, IEntity<TId>
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
```

---

### 3. البحث الذكي | Smart Search

#### ISmartSearchable
واجهة للكيانات القابلة للبحث الذكي.

```csharp
public interface ISmartSearchable
{
    string GetSearchableText();
}
```

**الاستخدام | Usage:**
```csharp
public class Product : IEntity<Guid>, ISmartSearchable
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Sku { get; set; }

    public string GetSearchableText()
    {
        return $"{Name} {Description} {Sku}";
    }
}
```

#### SmartSearchRequest
نموذج طلب البحث الذكي مع التصفية والترتيب والتصفح.

```csharp
public class SmartSearchRequest
{
    public string? Query { get; set; }
    public Dictionary<string, string>? Filters { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
```

#### SmartSearchResult<T>
نتيجة البحث الذكي مع معلومات التصفح.

```csharp
public class SmartSearchResult<T>
{
    public IReadOnlyList<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

---

### 4. الأحداث النطاقية | Domain Events

#### IDomainEvent
واجهة لتعريف الأحداث النطاقية.

```csharp
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
```

#### IHasDomainEvents
واجهة للكيانات التي تنتج أحداث نطاقية.

```csharp
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void AddDomainEvent(IDomainEvent domainEvent);
    void RemoveDomainEvent(IDomainEvent domainEvent);
    void ClearDomainEvents();
}
```

**مثال على الاستخدام | Usage Example:**
```csharp
public class Order : IEntity<Guid>, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; set; }
    public OrderStatus Status { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void Complete()
    {
        Status = OrderStatus.Completed;
        AddDomainEvent(new OrderCompletedEvent(Id));
    }

    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(IDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

---

### 5. وحدة العمل | Unit of Work

#### IUnitOfWork
واجهة لإدارة المعاملات وحفظ التغييرات.

```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
```

**الاستخدام | Usage:**
```csharp
public class CreateOrderHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IRepository<OrderItem, Guid> _itemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public async Task Handle(CreateOrderCommand command)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var order = new Order { /* ... */ };
            await _orderRepository.AddAsync(order);

            foreach (var item in command.Items)
            {
                await _itemRepository.AddAsync(new OrderItem { /* ... */ });
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
```

---

### 6. نتائج العمليات | Operation Results

#### Result<T>
نمط النتيجة للتعامل مع النجاح والفشل بدون استثناءات.

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? Error { get; }
    public IReadOnlyList<string> Errors { get; }

    public static Result<T> Success(T value);
    public static Result<T> Failure(string error);
    public static Result<T> Failure(IEnumerable<string> errors);
}
```

**الاستخدام | Usage:**
```csharp
public async Task<Result<Order>> CreateOrder(CreateOrderDto dto)
{
    if (dto.Items.Count == 0)
        return Result<Order>.Failure("Order must have at least one item");

    var order = new Order { /* ... */ };
    await _repository.AddAsync(order);

    return Result<Order>.Success(order);
}

// في الاستخدام | In usage:
var result = await CreateOrder(dto);
if (result.IsFailure)
{
    _logger.LogError("Failed to create order: {Error}", result.Error);
    return BadRequest(result.Error);
}

return Ok(result.Value);
```

---

### 7. المواصفات | Specifications

#### ISpecification<T>
نمط المواصفات لتغليف منطق الاستعلام.

```csharp
public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    List<string> IncludeStrings { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    int? Take { get; }
    int? Skip { get; }
    bool IsPagingEnabled { get; }
}
```

**مثال على الاستخدام | Usage Example:**
```csharp
public class ActiveProductsSpecification : BaseSpecification<Product>
{
    public ActiveProductsSpecification(string? category = null)
        : base(p => p.IsActive && (category == null || p.Category == category))
    {
        AddInclude(p => p.Images);
        AddInclude(p => p.Variants);
        ApplyOrderByDescending(p => p.CreatedAt);
    }
}

// الاستخدام | Usage:
var spec = new ActiveProductsSpecification("Electronics");
var products = await _repository.ListAsync(spec);
```

---

## أنماط التصميم المستخدمة | Design Patterns Used

### 1. Repository Pattern
فصل منطق الوصول للبيانات عن منطق الأعمال.

### 2. Unit of Work Pattern
إدارة المعاملات عبر مستودعات متعددة.

### 3. Specification Pattern
تغليف منطق الاستعلام في كائنات قابلة لإعادة الاستخدام.

### 4. Result Pattern
التعامل مع النجاح والفشل بدون استثناءات.

### 5. Domain Events Pattern
فصل الآثار الجانبية عن العمليات الرئيسية.

---

## أفضل الممارسات | Best Practices

### 1. استخدام الواجهات دائماً
```csharp
// ✅ صحيح | Correct
public class OrderService
{
    private readonly IRepository<Order, Guid> _repository;
}

// ❌ خاطئ | Wrong
public class OrderService
{
    private readonly EfCoreRepository<Order, Guid> _repository;
}
```

### 2. تنفيذ IAuditableEntity للكيانات المهمة
```csharp
public class Order : IEntity<Guid>, IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; }

    // Auditing
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // Soft Delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

### 3. استخدام Result<T> بدلاً من الاستثناءات
```csharp
// ✅ صحيح | Correct
public Task<Result<Order>> CreateOrder(CreateOrderDto dto);

// ❌ خاطئ للعمليات العادية | Wrong for normal operations
public Task<Order> CreateOrder(CreateOrderDto dto); // يرمي استثناء عند الفشل
```

### 4. تنفيذ ISmartSearchable للبحث
```csharp
public class Product : ISmartSearchable
{
    public string GetSearchableText()
    {
        return $"{Name} {Description} {Sku} {Brand}";
    }
}
```

---

## التكامل مع المكتبات الأخرى | Integration with Other Libraries

```
ACommerce.SharedKernel.Abstractions
           ↓
    ┌──────┴──────┐
    ↓             ↓
  CQRS    Infrastructure.EFCore
    ↓             ↓
    └──────┬──────┘
           ↓
    All Domain Libraries
    (Authentication, Catalog, Orders, etc.)
```

---

## المراجع | References

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Domain-Driven Design by Eric Evans](https://domainlanguage.com/ddd/)
- [Repository Pattern](https://martinfowler.com/eaaCatalog/repository.html)
- [Specification Pattern](https://martinfowler.com/apsupp/spec.pdf)
