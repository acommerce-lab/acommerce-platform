# أفضل الممارسات لمكتبات ACommerce | ACommerce Best Practices

## مقدمة | Introduction

هذه المقالة تشرح أفضل الممارسات للاستفادة القصوى من مكتبات ACommerce. تغطي موضوعات مهمة مثل تعدد المستأجرين، وسير العمل، والخصائص الديناميكية، وموديولات التقييم، والترجمة، وغيرها.

This article explains best practices for getting the most out of ACommerce libraries. It covers important topics like multi-tenancy, workflows, dynamic attributes, evaluation modules, localization, and more.

---

## 1. تعدد المستأجرين | Multi-Tenancy

### ما هو تعدد المستأجرين؟

تعدد المستأجرين يعني أن تطبيق واحد يخدم عدة عملاء (مستأجرين) مع عزل البيانات بينهم.

```
┌─────────────────────────────────────────────────────────────────┐
│                    Single Application Instance                   │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │
│  │  Tenant A   │  │  Tenant B   │  │  Tenant C   │            │
│  │  (Store 1)  │  │  (Store 2)  │  │  (Store 3)  │            │
│  │             │  │             │  │             │            │
│  │ - Products  │  │ - Products  │  │ - Products  │            │
│  │ - Orders    │  │ - Orders    │  │ - Orders    │            │
│  │ - Customers │  │ - Customers │  │ - Customers │            │
│  └─────────────┘  └─────────────┘  └─────────────┘            │
└─────────────────────────────────────────────────────────────────┘
```

### التنفيذ في ACommerce

#### 1. تنفيذ IMultiTenantEntity

```csharp
public class Product : IEntity<Guid>, IMultiTenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }  // معرف المستأجر

    public string Name { get; set; }
    public decimal Price { get; set; }
    // ...
}
```

#### 2. إنشاء TenantService

```csharp
public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _tenantId;

    public TenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetCurrentTenantId()
    {
        if (_tenantId.HasValue)
            return _tenantId;

        // استخراج من JWT Claim
        var tenantClaim = _httpContextAccessor.HttpContext?.User
            .FindFirst("tenant_id");

        if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tenantId))
            return tenantId;

        // أو من Header
        var tenantHeader = _httpContextAccessor.HttpContext?.Request
            .Headers["X-Tenant-Id"].FirstOrDefault();

        if (!string.IsNullOrEmpty(tenantHeader) && Guid.TryParse(tenantHeader, out tenantId))
            return tenantId;

        // أو من Subdomain
        var host = _httpContextAccessor.HttpContext?.Request.Host.Host;
        if (!string.IsNullOrEmpty(host))
        {
            // store1.myeshop.com -> store1
            var subdomain = host.Split('.').FirstOrDefault();
            // Lookup tenant by subdomain...
        }

        return null;
    }

    public void SetCurrentTenant(Guid tenantId)
    {
        _tenantId = tenantId;
    }
}
```

#### 3. التصفية التلقائية (Global Query Filter)

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // تطبيق فلتر المستأجر على جميع الكيانات
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(IMultiTenantEntity).IsAssignableFrom(entityType.ClrType))
        {
            var tenantId = _tenantService?.GetCurrentTenantId();
            if (tenantId.HasValue)
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, "TenantId");
                var constant = Expression.Constant(tenantId.Value);
                var filter = Expression.Lambda(
                    Expression.Equal(property, constant),
                    parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }
}
```

#### 4. التعيين التلقائي للـ TenantId

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    var tenantId = _tenantService?.GetCurrentTenantId();

    if (tenantId.HasValue)
    {
        var entries = ChangeTracker.Entries<IMultiTenantEntity>()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in entries)
        {
            entry.Entity.TenantId = tenantId.Value;
        }
    }

    return await base.SaveChangesAsync(cancellationToken);
}
```

### أفضل الممارسات لتعدد المستأجرين

| الممارسة | الوصف |
|----------|-------|
| ✅ استخدم Index على TenantId | تحسين الأداء للاستعلامات |
| ✅ تحقق من TenantId في العمليات الحساسة | أمان إضافي |
| ✅ استخدم Middleware للتعيين المبكر | تحديد المستأجر قبل أي معالجة |
| ❌ لا تعرض TenantId للعملاء | تجنب تسرب المعلومات |
| ❌ لا تسمح بتغيير TenantId | منع الوصول غير المصرح |

---

## 2. سلاسل العمل | Workflows

### نمط State Machine للطلبات

```csharp
public class Order
{
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    // Allowed transitions
    private static readonly Dictionary<OrderStatus, OrderStatus[]> _transitions = new()
    {
        [OrderStatus.Pending] = new[] {
            OrderStatus.Confirmed,
            OrderStatus.Cancelled,
            OrderStatus.AwaitingPayment
        },
        [OrderStatus.AwaitingPayment] = new[] {
            OrderStatus.Paid,
            OrderStatus.PaymentFailed,
            OrderStatus.Cancelled
        },
        [OrderStatus.Paid] = new[] {
            OrderStatus.Processing
        },
        [OrderStatus.Confirmed] = new[] {
            OrderStatus.Processing,
            OrderStatus.Cancelled
        },
        [OrderStatus.Processing] = new[] {
            OrderStatus.Shipped
        },
        [OrderStatus.Shipped] = new[] {
            OrderStatus.Delivered,
            OrderStatus.Failed
        },
        [OrderStatus.Delivered] = new[] {
            OrderStatus.Completed,
            OrderStatus.ReturnRequested
        },
        [OrderStatus.ReturnRequested] = new[] {
            OrderStatus.Returned,
            OrderStatus.Completed
        }
    };

    public Result TransitionTo(OrderStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
        {
            return Result.Failure(
                $"لا يمكن الانتقال من {Status} إلى {newStatus}");
        }

        var oldStatus = Status;
        Status = newStatus;

        // Add domain event
        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, newStatus));

        return Result.Success();
    }

    public bool CanTransitionTo(OrderStatus newStatus)
    {
        return _transitions.TryGetValue(Status, out var allowed) &&
               allowed.Contains(newStatus);
    }

    public IReadOnlyList<OrderStatus> GetAllowedTransitions()
    {
        return _transitions.TryGetValue(Status, out var allowed)
            ? allowed.ToList()
            : Array.Empty<OrderStatus>();
    }
}
```

### Workflow Service

```csharp
public interface IOrderWorkflowService
{
    Task<Result> ConfirmAsync(Guid orderId, CancellationToken ct = default);
    Task<Result> MarkAsPaidAsync(Guid orderId, string transactionId, CancellationToken ct = default);
    Task<Result> StartProcessingAsync(Guid orderId, CancellationToken ct = default);
    Task<Result> ShipAsync(Guid orderId, string trackingNumber, string carrier, CancellationToken ct = default);
    Task<Result> DeliverAsync(Guid orderId, CancellationToken ct = default);
    Task<Result> CompleteAsync(Guid orderId, CancellationToken ct = default);
    Task<Result> CancelAsync(Guid orderId, string reason, CancellationToken ct = default);
}

public class OrderWorkflowService : IOrderWorkflowService
{
    private readonly IRepository<Order, Guid> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderWorkflowService> _logger;

    public async Task<Result> ShipAsync(
        Guid orderId,
        string trackingNumber,
        string carrier,
        CancellationToken ct = default)
    {
        var order = await _repository.GetByIdAsync(orderId, ct);

        if (order == null)
            return Result.Failure("الطلب غير موجود");

        var result = order.Ship(trackingNumber, carrier);

        if (result.IsFailure)
            return result;

        await _unitOfWork.SaveChangesAsync(ct);

        // نشر الحدث
        await _eventBus.PublishAsync(new OrderShippedEvent(
            order.Id,
            order.OrderNumber,
            trackingNumber,
            carrier,
            DateTime.UtcNow));

        _logger.LogInformation(
            "Order {OrderNumber} shipped with tracking {TrackingNumber}",
            order.OrderNumber,
            trackingNumber);

        return Result.Success();
    }
}
```

### أفضل ممارسات سير العمل

1. **استخدم Domain Events** - لفصل الآثار الجانبية عن التحولات
2. **سجل جميع التحولات** - للتدقيق والتتبع
3. **تحقق من الصلاحيات** - قبل كل تحول
4. **استخدم Transactions** - لضمان الاتساق

---

## 3. الخصائص الديناميكية | Dynamic Attributes

### متى تستخدم الخصائص الديناميكية؟

| الحالة | استخدم خصائص ديناميكية؟ |
|--------|------------------------|
| خصائص تختلف حسب التصنيف | ✅ نعم |
| خصائص يحددها المستخدم | ✅ نعم |
| خصائص ثابتة لجميع المنتجات | ❌ لا - استخدم حقول عادية |
| خصائص تحتاج حسابات معقدة | ❌ لا - استخدم خدمات |

### تصميم نظام الخصائص

```csharp
// 1. تعريف الخاصية على مستوى التصنيف
public class CategoryAttributeTemplate
{
    public Guid CategoryId { get; set; }
    public Guid AttributeDefinitionId { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
}

// 2. عند إنشاء منتج في تصنيف معين
public async Task<Result<Guid>> CreateProductAsync(CreateProductCommand command)
{
    // الحصول على الخصائص المطلوبة للتصنيف
    var requiredAttributes = await _attributeRepository
        .GetByCategoryIdAsync(command.CategoryId);

    // التحقق من توفر جميع الخصائص المطلوبة
    var missingAttributes = requiredAttributes
        .Where(a => a.IsRequired && !command.Attributes.ContainsKey(a.Id))
        .ToList();

    if (missingAttributes.Any())
    {
        return Result<Guid>.Failure(
            $"الخصائص التالية مطلوبة: {string.Join(", ", missingAttributes.Select(a => a.Name))}");
    }

    // إنشاء المنتج مع الخصائص
    var product = new Product { /* ... */ };

    foreach (var (attrId, value) in command.Attributes)
    {
        var definition = await _attributeDefinitionRepository.GetByIdAsync(attrId);
        var attribute = new ProductAttribute
        {
            ProductId = product.Id,
            AttributeDefinitionId = attrId
        };
        attribute.SetValue(value);

        product.Attributes.Add(attribute);
    }

    await _productRepository.AddAsync(product);
    return Result<Guid>.Success(product.Id);
}
```

### البحث بالخصائص

```csharp
public async Task<SmartSearchResult<ProductListDto>> SearchWithAttributesAsync(
    string? searchTerm,
    Dictionary<string, List<string>>? attributeFilters,
    int page = 1,
    int pageSize = 20)
{
    var query = _context.Products
        .Include(p => p.Attributes)
        .ThenInclude(a => a.Definition)
        .AsQueryable();

    // فلترة بالنص
    if (!string.IsNullOrEmpty(searchTerm))
    {
        query = query.Where(p =>
            p.Name.Contains(searchTerm) ||
            p.Description.Contains(searchTerm));
    }

    // فلترة بالخصائص
    if (attributeFilters != null)
    {
        foreach (var filter in attributeFilters)
        {
            var attributeCode = filter.Key;
            var values = filter.Value;

            query = query.Where(p =>
                p.Attributes.Any(a =>
                    a.Definition.Code == attributeCode &&
                    values.Contains(a.TextValue)));
        }
    }

    // الحصول على العدد الكلي
    var total = await query.CountAsync();

    // التصفح
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(p => new ProductListDto
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.BasePrice,
            // ...
        })
        .ToListAsync();

    return new SmartSearchResult<ProductListDto>
    {
        Items = items,
        TotalCount = total,
        Page = page,
        PageSize = pageSize
    };
}
```

---

## 4. موديولات التقييم | Evaluation Modules

### نظام تقييم المنتجات

```csharp
public class ProductReview : IEntity<Guid>, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? OrderId { get; set; }  // للتحقق من الشراء

    public int Rating { get; set; }  // 1-5
    public string? Title { get; set; }
    public string? Content { get; set; }

    public bool IsVerifiedPurchase { get; set; }
    public bool IsApproved { get; set; }

    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }

    // الصور المرفقة
    public List<string> ImageUrls { get; set; } = new();

    // Audit
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
```

### حساب متوسط التقييم

```csharp
public interface IProductRatingService
{
    Task<ProductRatingStats> GetRatingStatsAsync(Guid productId, CancellationToken ct = default);
    Task UpdateProductRatingAsync(Guid productId, CancellationToken ct = default);
}

public class ProductRatingService : IProductRatingService
{
    private readonly IRepository<ProductReview, Guid> _reviewRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<ProductRatingStats> GetRatingStatsAsync(
        Guid productId,
        CancellationToken ct = default)
    {
        var reviews = await _reviewRepository.FindAsync(
            r => r.ProductId == productId && r.IsApproved,
            ct);

        if (!reviews.Any())
        {
            return new ProductRatingStats
            {
                ProductId = productId,
                AverageRating = 0,
                TotalReviews = 0,
                RatingDistribution = new Dictionary<int, int>
                {
                    [1] = 0, [2] = 0, [3] = 0, [4] = 0, [5] = 0
                }
            };
        }

        var distribution = reviews
            .GroupBy(r => r.Rating)
            .ToDictionary(g => g.Key, g => g.Count());

        // Fill missing ratings
        for (int i = 1; i <= 5; i++)
        {
            distribution.TryAdd(i, 0);
        }

        return new ProductRatingStats
        {
            ProductId = productId,
            AverageRating = reviews.Average(r => r.Rating),
            TotalReviews = reviews.Count,
            RatingDistribution = distribution,
            VerifiedPurchaseCount = reviews.Count(r => r.IsVerifiedPurchase)
        };
    }

    public async Task UpdateProductRatingAsync(Guid productId, CancellationToken ct = default)
    {
        var stats = await GetRatingStatsAsync(productId, ct);

        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product != null)
        {
            product.AverageRating = stats.AverageRating;
            product.ReviewCount = stats.TotalReviews;

            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}

public class ProductRatingStats
{
    public Guid ProductId { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
    public int VerifiedPurchaseCount { get; set; }
}
```

---

## 5. الترجمة والتعريب | Localization

### نظام الترجمة للكيانات

```csharp
public interface ILocalizable<TTranslation>
    where TTranslation : class, ITranslation
{
    ICollection<TTranslation> Translations { get; }
}

public interface ITranslation
{
    string LanguageCode { get; }
}

public class Product : IEntity<Guid>, ILocalizable<ProductTranslation>
{
    public Guid Id { get; set; }

    // القيمة الافتراضية (عادة اللغة الأساسية)
    public string Name { get; set; }
    public string? Description { get; set; }

    // الترجمات
    public ICollection<ProductTranslation> Translations { get; set; } = new List<ProductTranslation>();

    // Helper method
    public string GetLocalizedName(string languageCode)
    {
        var translation = Translations.FirstOrDefault(t => t.LanguageCode == languageCode);
        return translation?.Name ?? Name;
    }
}

public class ProductTranslation : ITranslation
{
    public Guid ProductId { get; set; }
    public string LanguageCode { get; set; }  // ar, en, fr

    public string Name { get; set; }
    public string? Description { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
}
```

### خدمة الترجمة

```csharp
public interface ILocalizationService
{
    string CurrentLanguage { get; }
    Task<T?> GetLocalizedAsync<T, TTranslation>(
        Guid entityId,
        CancellationToken ct = default)
        where T : class, ILocalizable<TTranslation>
        where TTranslation : class, ITranslation;
}

public class LocalizationService : ILocalizationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public string CurrentLanguage
    {
        get
        {
            // من Header
            var acceptLanguage = _httpContextAccessor.HttpContext?.Request
                .Headers["Accept-Language"].FirstOrDefault();

            // أو من Query String
            var queryLang = _httpContextAccessor.HttpContext?.Request
                .Query["lang"].FirstOrDefault();

            // أو من المستخدم المسجل
            var userLang = _httpContextAccessor.HttpContext?.User
                .FindFirst("preferred_language")?.Value;

            return queryLang ?? userLang ?? ParseAcceptLanguage(acceptLanguage) ?? "ar";
        }
    }

    private string? ParseAcceptLanguage(string? header)
    {
        if (string.IsNullOrEmpty(header)) return null;

        // Parse "ar-SA,ar;q=0.9,en;q=0.8"
        var languages = header.Split(',')
            .Select(l => l.Split(';')[0].Trim())
            .ToList();

        return languages.FirstOrDefault();
    }
}
```

### استعلام مع الترجمة

```csharp
public async Task<ProductDto?> GetProductLocalizedAsync(
    Guid id,
    string languageCode,
    CancellationToken ct = default)
{
    var product = await _context.Products
        .Include(p => p.Translations)
        .FirstOrDefaultAsync(p => p.Id == id, ct);

    if (product == null) return null;

    var translation = product.Translations
        .FirstOrDefault(t => t.LanguageCode == languageCode);

    return new ProductDto
    {
        Id = product.Id,
        Name = translation?.Name ?? product.Name,
        Description = translation?.Description ?? product.Description,
        // ...
    };
}
```

---

## 6. تعدد المزودين | Multi-Provider Pattern

### نمط المزودين للدفع

```csharp
public interface IPaymentProviderFactory
{
    IPaymentService GetProvider(string providerName);
    IEnumerable<PaymentProviderInfo> GetAvailableProviders();
}

public class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _providers;

    public PaymentProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _providers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["Moyasar"] = typeof(MoyasarPaymentService),
            ["Stripe"] = typeof(StripePaymentService),
            ["PayPal"] = typeof(PayPalPaymentService),
            ["COD"] = typeof(CodPaymentService)
        };
    }

    public IPaymentService GetProvider(string providerName)
    {
        if (!_providers.TryGetValue(providerName, out var providerType))
        {
            throw new InvalidOperationException(
                $"Payment provider '{providerName}' is not registered");
        }

        return (IPaymentService)_serviceProvider.GetRequiredService(providerType);
    }

    public IEnumerable<PaymentProviderInfo> GetAvailableProviders()
    {
        return _providers.Keys.Select(name => new PaymentProviderInfo
        {
            Name = name,
            IsAvailable = IsProviderConfigured(name)
        });
    }

    private bool IsProviderConfigured(string providerName)
    {
        // Check if provider has required configuration
        var config = _serviceProvider.GetService<IConfiguration>();
        return !string.IsNullOrEmpty(
            config?[$"Payments:{providerName}:SecretKey"]);
    }
}
```

### تسجيل المزودين

```csharp
public static class PaymentServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register factory
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();

        // Register individual providers
        if (HasConfiguration(configuration, "Payments:Moyasar"))
        {
            services.AddMoyasarPayments(configuration);
        }

        if (HasConfiguration(configuration, "Payments:Stripe"))
        {
            services.AddStripePayments(configuration);
        }

        if (HasConfiguration(configuration, "Payments:PayPal"))
        {
            services.AddPayPalPayments(configuration);
        }

        // COD is always available
        services.AddScoped<CodPaymentService>();

        return services;
    }

    private static bool HasConfiguration(IConfiguration config, string section)
    {
        return config.GetSection(section).Exists();
    }
}
```

---

## 7. أمان API | API Security

### التحقق من الصلاحيات

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    // قراءة - للجميع
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> GetProducts() { }

    // إنشاء - للمشرفين فقط
    [HttpPost]
    [Authorize(Roles = "Admin,ProductManager")]
    public async Task<ActionResult> CreateProduct() { }

    // تحديث - للمشرفين أو مالك المنتج
    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult> UpdateProduct(Guid id)
    {
        var product = await _productService.GetByIdAsync(id);

        if (product == null)
            return NotFound();

        // التحقق من الملكية
        if (!User.IsInRole("Admin") && product.CreatedBy != User.GetUserId())
            return Forbid();

        // ...
    }
}
```

### Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    // حد عام
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // حد خاص لـ API
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            }));

    // حد خاص للمصادقة (أقل لمنع brute force)
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

---

## 8. التخزين المؤقت | Caching

### استراتيجيات التخزين المؤقت

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default);
}

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly JsonSerializerOptions _jsonOptions;

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);

        if (cached != null)
            return cached;

        var value = await factory();

        await SetAsync(key, value, expiration, ct);

        return value;
    }
}

// الاستخدام
public async Task<ProductDetailDto?> GetProductAsync(Guid id)
{
    var cacheKey = $"product:{id}";

    return await _cache.GetOrSetAsync(
        cacheKey,
        async () => await _productRepository.GetByIdAsync(id),
        expiration: TimeSpan.FromMinutes(10));
}
```

### إبطال الكاش

```csharp
public class ProductService
{
    public async Task UpdateProductAsync(UpdateProductCommand command)
    {
        await _repository.UpdateAsync(/* ... */);

        // إبطال الكاش
        await _cache.RemoveAsync($"product:{command.Id}");
        await _cache.RemoveAsync($"products:category:{command.CategoryId}");
        await _cache.RemoveAsync("products:featured");
    }
}
```

---

## 9. المراقبة والتسجيل | Monitoring & Logging

### Structured Logging

```csharp
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public async Task<Result<Guid>> CreateOrderAsync(CreateOrderCommand command)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CustomerId"] = command.CustomerId,
            ["ItemCount"] = command.Items.Count,
            ["Total"] = command.Items.Sum(i => i.Quantity * i.UnitPrice)
        });

        _logger.LogInformation("Creating order for customer {CustomerId}", command.CustomerId);

        try
        {
            var order = await CreateOrder(command);

            _logger.LogInformation(
                "Order {OrderId} created successfully with {ItemCount} items, total {Total}",
                order.Id,
                order.Items.Count,
                order.Total);

            return Result<Guid>.Success(order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create order for customer {CustomerId}",
                command.CustomerId);

            return Result<Guid>.Failure("فشل في إنشاء الطلب");
        }
    }
}
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")
    .AddRedis(connectionString, "redis")
    .AddRabbitMQ("rabbitmq")
    .AddCheck<PaymentGatewayHealthCheck>("payment-gateway");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds
            })
        });

        await context.Response.WriteAsync(result);
    }
});
```

---

## ملخص | Summary

| الموضوع | أهم النقاط |
|---------|-----------|
| تعدد المستأجرين | استخدم Global Query Filters + تعيين تلقائي |
| سير العمل | استخدم State Machine + Domain Events |
| الخصائص الديناميكية | تعريف على مستوى التصنيف + تحقق عند الإنشاء |
| التقييم | حساب تلقائي للمتوسط + التحقق من الشراء |
| الترجمة | جداول منفصلة للترجمات + Helper methods |
| تعدد المزودين | Factory Pattern + تسجيل ديناميكي |
| الأمان | Rate Limiting + Authorization + Input Validation |
| التخزين المؤقت | Get-Or-Set Pattern + إبطال ذكي |
| المراقبة | Structured Logging + Health Checks |

---

## المراجع | References

- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Domain-Driven Design](https://martinfowler.com/bliki/DomainDrivenDesign.html)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [OWASP Security Guidelines](https://owasp.org/www-project-web-security-testing-guide/)
