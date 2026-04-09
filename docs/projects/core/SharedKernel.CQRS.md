# ACommerce.SharedKernel.CQRS

## نظرة عامة | Overview

مكتبة `ACommerce.SharedKernel.CQRS` توفر البنية التحتية الكاملة لنمط CQRS (Command Query Responsibility Segregation) باستخدام MediatR. تتضمن السلوكيات المشتركة (Behaviors)، والتحقق من الصحة (Validation)، والتسجيل (Logging)، وإدارة المعاملات.

This library provides the complete infrastructure for the CQRS pattern using MediatR. It includes shared behaviors, validation, logging, and transaction management.

**المسار | Path:** `SharedKernel/ACommerce.SharedKernel.CQRS`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)
**الاعتماديات | Dependencies:**
- MediatR
- FluentValidation
- ACommerce.SharedKernel.Abstractions

---

## مفهوم CQRS | CQRS Concept

CQRS يفصل عمليات القراءة (Queries) عن عمليات الكتابة (Commands):

```
┌─────────────────────────────────────────────────────────┐
│                      Client Request                      │
└─────────────────────────────────────────────────────────┘
                            │
              ┌─────────────┴─────────────┐
              ↓                           ↓
     ┌─────────────────┐         ┌─────────────────┐
     │    Commands     │         │     Queries     │
     │   (Write Ops)   │         │   (Read Ops)    │
     └────────┬────────┘         └────────┬────────┘
              ↓                           ↓
     ┌─────────────────┐         ┌─────────────────┐
     │ Command Handler │         │  Query Handler  │
     └────────┬────────┘         └────────┬────────┘
              ↓                           ↓
     ┌─────────────────┐         ┌─────────────────┐
     │ Write Database  │         │ Read Database   │
     │   (or same DB)  │         │  (or same DB)   │
     └─────────────────┘         └─────────────────┘
```

---

## المكونات الرئيسية | Core Components

### 1. الأوامر | Commands

#### ICommand<TResponse>
واجهة لتعريف الأوامر التي تعدل الحالة.

```csharp
public interface ICommand<TResponse> : IRequest<TResponse>
{
}
```

#### ICommandHandler<TCommand, TResponse>
واجهة لمعالجات الأوامر.

```csharp
public interface ICommandHandler<TCommand, TResponse>
    : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}
```

**مثال كامل | Complete Example:**

```csharp
// تعريف الأمر | Command Definition
public record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    string Sku,
    Guid CategoryId
) : ICommand<Result<Guid>>;

// التحقق من الصحة | Validation
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المنتج مطلوب")
            .MaximumLength(200).WithMessage("اسم المنتج يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("السعر يجب أن يكون أكبر من صفر");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("رمز المنتج (SKU) مطلوب")
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("رمز المنتج يجب أن يحتوي على أحرف كبيرة وأرقام فقط");
    }
}

// معالج الأمر | Command Handler
public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Result<Guid>>
{
    private readonly IRepository<Product, Guid> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(
        IRepository<Product, Guid> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        // التحقق من عدم وجود SKU مكرر
        var existingProduct = await _repository.FindAsync(
            p => p.Sku == request.Sku, cancellationToken);

        if (existingProduct.Any())
            return Result<Guid>.Failure("رمز المنتج (SKU) موجود مسبقاً");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Sku = request.Sku,
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(product.Id);
    }
}
```

---

### 2. الاستعلامات | Queries

#### IQuery<TResponse>
واجهة لتعريف الاستعلامات للقراءة فقط.

```csharp
public interface IQuery<TResponse> : IRequest<TResponse>
{
}
```

#### IQueryHandler<TQuery, TResponse>
واجهة لمعالجات الاستعلامات.

```csharp
public interface IQueryHandler<TQuery, TResponse>
    : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
```

**مثال كامل | Complete Example:**

```csharp
// تعريف الاستعلام | Query Definition
public record GetProductByIdQuery(Guid Id) : IQuery<ProductDto?>;

// DTO للنتيجة | Result DTO
public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Sku,
    string CategoryName,
    List<string> ImageUrls,
    DateTime CreatedAt
);

// معالج الاستعلام | Query Handler
public class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly IReadOnlyRepository<Product, Guid> _repository;
    private readonly IMapper _mapper;

    public GetProductByIdQueryHandler(
        IReadOnlyRepository<Product, Guid> repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ProductDto?> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (product == null)
            return null;

        return _mapper.Map<ProductDto>(product);
    }
}
```

---

### 3. استعلامات البحث | Search Queries

#### SearchQuery<TResult>
استعلام بحث عام مع دعم التصفح والفرز.

```csharp
public record SearchQuery<TResult>(
    string? SearchTerm,
    Dictionary<string, string>? Filters,
    string? SortBy,
    bool SortDescending,
    int Page,
    int PageSize
) : IQuery<SmartSearchResult<TResult>>;
```

**مثال على استعلام بحث المنتجات | Product Search Example:**

```csharp
// تعريف الاستعلام | Query Definition
public record SearchProductsQuery(
    string? SearchTerm,
    Guid? CategoryId,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool? InStock,
    string? SortBy,
    bool SortDescending,
    int Page = 1,
    int PageSize = 20
) : IQuery<SmartSearchResult<ProductListDto>>;

// معالج الاستعلام | Query Handler
public class SearchProductsQueryHandler
    : IQueryHandler<SearchProductsQuery, SmartSearchResult<ProductListDto>>
{
    private readonly IReadOnlyRepository<Product, Guid> _repository;
    private readonly IMapper _mapper;

    public async Task<SmartSearchResult<ProductListDto>> Handle(
        SearchProductsQuery request,
        CancellationToken cancellationToken)
    {
        // بناء الاستعلام
        Expression<Func<Product, bool>> predicate = p => p.IsActive;

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            predicate = predicate.And(p =>
                p.Name.ToLower().Contains(term) ||
                p.Description.ToLower().Contains(term) ||
                p.Sku.ToLower().Contains(term));
        }

        if (request.CategoryId.HasValue)
            predicate = predicate.And(p => p.CategoryId == request.CategoryId);

        if (request.MinPrice.HasValue)
            predicate = predicate.And(p => p.Price >= request.MinPrice);

        if (request.MaxPrice.HasValue)
            predicate = predicate.And(p => p.Price <= request.MaxPrice);

        if (request.InStock.HasValue)
            predicate = predicate.And(p => p.StockQuantity > 0 == request.InStock);

        // تنفيذ الاستعلام
        var products = await _repository.FindAsync(predicate, cancellationToken);

        // الفرز
        var sorted = request.SortBy?.ToLower() switch
        {
            "price" => request.SortDescending
                ? products.OrderByDescending(p => p.Price)
                : products.OrderBy(p => p.Price),
            "name" => request.SortDescending
                ? products.OrderByDescending(p => p.Name)
                : products.OrderBy(p => p.Name),
            "date" => request.SortDescending
                ? products.OrderByDescending(p => p.CreatedAt)
                : products.OrderBy(p => p.CreatedAt),
            _ => products.OrderByDescending(p => p.CreatedAt)
        };

        // التصفح
        var total = sorted.Count();
        var items = sorted
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new SmartSearchResult<ProductListDto>
        {
            Items = _mapper.Map<List<ProductListDto>>(items),
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
```

---

### 4. السلوكيات | Behaviors (Pipeline)

السلوكيات هي middleware تعترض الطلبات قبل وبعد المعالجة.

```
Request → [Logging] → [Validation] → [Transaction] → Handler → Response
                                                         ↓
Response ← [Logging] ← [Validation] ← [Transaction] ← ───┘
```

#### ValidationBehavior
التحقق من صحة الطلبات تلقائياً.

```csharp
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

#### LoggingBehavior
تسجيل جميع الطلبات والاستجابات.

```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestGuid = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[START] {RequestName} [{RequestGuid}] {@Request}",
            requestName, requestGuid, request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();

            _logger.LogInformation(
                "[END] {RequestName} [{RequestGuid}] - {ElapsedMs}ms",
                requestName, requestGuid, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "[ERROR] {RequestName} [{RequestGuid}] - {ElapsedMs}ms - {Message}",
                requestName, requestGuid, stopwatch.ElapsedMilliseconds, ex.Message);

            throw;
        }
    }
}
```

#### TransactionBehavior
إدارة المعاملات تلقائياً للأوامر.

```csharp
public class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation(
            "[TRANSACTION START] {RequestName}",
            requestName);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next();

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "[TRANSACTION COMMITTED] {RequestName}",
                requestName);

            return response;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);

            _logger.LogWarning(
                "[TRANSACTION ROLLED BACK] {RequestName}",
                requestName);

            throw;
        }
    }
}
```

#### PerformanceBehavior
مراقبة الأداء والتحذير من الاستعلامات البطيئة.

```csharp
public class PerformanceBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly int _slowThresholdMs;

    public PerformanceBehavior(
        ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
        IOptions<PerformanceOptions> options)
    {
        _logger = logger;
        _slowThresholdMs = options.Value.SlowQueryThresholdMs;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > _slowThresholdMs)
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogWarning(
                "[SLOW REQUEST] {RequestName} took {ElapsedMs}ms (Threshold: {ThresholdMs}ms)",
                requestName,
                stopwatch.ElapsedMilliseconds,
                _slowThresholdMs);
        }

        return response;
    }
}
```

---

### 5. AutoMapper Integration

#### تكوين AutoMapper | AutoMapper Configuration

```csharp
public class ProductMappingProfile : Profile
{
    public ProductMappingProfile()
    {
        CreateMap<Product, ProductDto>()
            .ForMember(dest => dest.CategoryName,
                opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.ImageUrls,
                opt => opt.MapFrom(src => src.Images.Select(i => i.Url).ToList()));

        CreateMap<Product, ProductListDto>()
            .ForMember(dest => dest.MainImageUrl,
                opt => opt.MapFrom(src => src.Images.FirstOrDefault().Url));

        CreateMap<CreateProductCommand, Product>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());
    }
}
```

---

## تسجيل الخدمات | Service Registration

### AddCQRS Extension Method

```csharp
public static class CqrsServiceCollectionExtensions
{
    public static IServiceCollection AddCqrs(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        // Register MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies);
        });

        // Register Validators
        services.AddValidatorsFromAssemblies(assemblies);

        // Register AutoMapper
        services.AddAutoMapper(assemblies);

        // Register Behaviors
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));

        return services;
    }

    public static IServiceCollection AddCqrsWithTransaction(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        services.AddCqrs(assemblies);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
```

### الاستخدام في Program.cs | Usage in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add CQRS with all behaviors
builder.Services.AddCqrsWithTransaction(
    typeof(CreateProductCommand).Assembly,  // Commands
    typeof(GetProductByIdQuery).Assembly    // Queries
);

// Configure performance options
builder.Services.Configure<PerformanceOptions>(options =>
{
    options.SlowQueryThresholdMs = 500; // Log queries taking more than 500ms
});
```

---

## الاستخدام في Controllers | Usage in Controllers

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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(id));

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<SmartSearchResult<ProductListDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] Guid? category,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? sort,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var query = new SearchProductsQuery(
            SearchTerm: q,
            CategoryId: category,
            MinPrice: minPrice,
            MaxPrice: maxPrice,
            InStock: null,
            SortBy: sort,
            SortDescending: desc,
            Page: page,
            PageSize: size);

        var result = await _mediator.Send(query);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProductCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value },
            result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command)
    {
        if (id != command.Id)
            return BadRequest("ID mismatch");

        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteProductCommand(id));

        if (result.IsFailure)
            return BadRequest(result.Error);

        return NoContent();
    }
}
```

---

## أفضل الممارسات | Best Practices

### 1. فصل Commands عن Queries
```
📁 Features/
   📁 Products/
      📁 Commands/
         📄 CreateProduct.cs
         📄 UpdateProduct.cs
         📄 DeleteProduct.cs
      📁 Queries/
         📄 GetProductById.cs
         📄 SearchProducts.cs
         📄 GetProductsByCategory.cs
```

### 2. استخدام Records للأوامر والاستعلامات
```csharp
// ✅ مفضل | Preferred
public record CreateProductCommand(string Name, decimal Price) : ICommand<Result<Guid>>;

// ❌ غير مفضل | Not Preferred
public class CreateProductCommand : ICommand<Result<Guid>>
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}
```

### 3. إرجاع Result<T> من الأوامر
```csharp
// ✅ صحيح | Correct
public record CreateProductCommand(...) : ICommand<Result<Guid>>;

// ❌ يفقد معلومات الخطأ | Loses error information
public record CreateProductCommand(...) : ICommand<Guid>;
```

### 4. التحقق من الصحة في Validator منفصل
```csharp
// ✅ صحيح | Correct - منفصل
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

// ❌ خاطئ | Wrong - داخل Handler
public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand request, ...)
    {
        if (string.IsNullOrEmpty(request.Name))
            return Result<Guid>.Failure("Name is required");
        // ...
    }
}
```

---

## التكامل مع المكتبات الأخرى | Integration

```
ACommerce.SharedKernel.Abstractions
              ↓
   ACommerce.SharedKernel.CQRS
              ↓
    ┌─────────┴─────────┐
    ↓                   ↓
 Domain APIs      Domain Services
 (Controllers)    (Application Layer)
```

---

## المراجع | References

- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [AutoMapper Documentation](https://automapper.org/)
