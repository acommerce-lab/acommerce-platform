# ACommerce.Catalog.Products

## نظرة عامة | Overview

مكتبة `ACommerce.Catalog.Products` توفر نظام إدارة المنتجات الكامل مع دعم المتغيرات (Variants)، والصور، والخصائص الديناميكية، والمخزون، والتسعير المتقدم.

This library provides a complete product management system with support for variants, images, dynamic attributes, inventory, and advanced pricing.

**المسار | Path:** `Catalog/ACommerce.Catalog.Products`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)
**الاعتماديات | Dependencies:**
- ACommerce.SharedKernel.Abstractions
- ACommerce.SharedKernel.CQRS
- ACommerce.Catalog.Attributes
- ACommerce.Catalog.Categories
- ACommerce.Catalog.Currencies

---

## نموذج البيانات | Data Model

### Product Entity

```csharp
public class Product : IEntity<Guid>, IAuditableEntity, ISoftDeletable, IMultiTenantEntity, ISmartSearchable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // Basic Information
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }

    // Categorization
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }

    // Pricing
    public decimal BasePrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public Guid CurrencyId { get; set; }
    public Currency? Currency { get; set; }

    // Inventory
    public int StockQuantity { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool TrackInventory { get; set; } = true;
    public bool AllowBackorder { get; set; }
    public InventoryPolicy InventoryPolicy { get; set; } = InventoryPolicy.DenyWhenOutOfStock;

    // Shipping
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public WeightUnit WeightUnit { get; set; } = WeightUnit.Kilogram;
    public DimensionUnit DimensionUnit { get; set; } = DimensionUnit.Centimeter;
    public bool RequiresShipping { get; set; } = true;

    // Status
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime? PublishedAt { get; set; }

    // SEO
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }

    // Relations
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductAttribute> Attributes { get; set; } = new List<ProductAttribute>();
    public ICollection<ProductTag> Tags { get; set; } = new List<ProductTag>();

    // Audit & Soft Delete
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    // ISmartSearchable
    public string GetSearchableText()
    {
        return $"{Name} {ShortDescription} {Sku} {Barcode} {Brand?.Name} {Category?.Name}";
    }
}
```

### ProductVariant Entity

```csharp
public class ProductVariant : IEntity<Guid>, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    // Identification
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string Title { get; set; } = string.Empty;

    // Variant Options (e.g., Color: Red, Size: XL)
    public Dictionary<string, string> Options { get; set; } = new();

    // Pricing (overrides base product if set)
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? CostPrice { get; set; }

    // Inventory
    public int StockQuantity { get; set; }
    public bool TrackInventory { get; set; } = true;

    // Shipping
    public decimal? Weight { get; set; }

    // Image
    public Guid? ImageId { get; set; }
    public ProductImage? Image { get; set; }

    // Status
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
```

### ProductImage Entity

```csharp
public class ProductImage : IEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }

    // Image dimensions
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? FileSize { get; set; }
}
```

### ProductAttribute Entity

```csharp
public class ProductAttribute : IEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid AttributeDefinitionId { get; set; }
    public AttributeDefinition Definition { get; set; } = null!;

    // Value storage (polymorphic based on attribute type)
    public string? TextValue { get; set; }
    public decimal? NumberValue { get; set; }
    public bool? BooleanValue { get; set; }
    public DateTime? DateValue { get; set; }
    public List<string>? MultiSelectValues { get; set; }
}
```

---

## Enums

```csharp
public enum ProductStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2,
    OutOfStock = 3
}

public enum InventoryPolicy
{
    DenyWhenOutOfStock = 0,
    AllowBackorder = 1,
    ContinueSelling = 2
}

public enum WeightUnit
{
    Kilogram = 0,
    Gram = 1,
    Pound = 2,
    Ounce = 3
}

public enum DimensionUnit
{
    Centimeter = 0,
    Meter = 1,
    Inch = 2,
    Foot = 3
}
```

---

## الأوامر | Commands

### CreateProductCommand

```csharp
public record CreateProductCommand(
    string Name,
    string Sku,
    decimal BasePrice,
    Guid CurrencyId,
    string? ShortDescription = null,
    string? LongDescription = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    decimal? CompareAtPrice = null,
    int StockQuantity = 0,
    bool TrackInventory = true,
    ProductStatus Status = ProductStatus.Draft,
    List<CreateProductImageDto>? Images = null,
    List<CreateProductVariantDto>? Variants = null,
    Dictionary<Guid, object>? Attributes = null
) : ICommand<Result<Guid>>;

public record CreateProductImageDto(
    string Url,
    string? AltText = null,
    bool IsPrimary = false,
    int DisplayOrder = 0
);

public record CreateProductVariantDto(
    string Title,
    string? Sku = null,
    Dictionary<string, string>? Options = null,
    decimal? Price = null,
    int StockQuantity = 0
);
```

### CreateProductCommandHandler

```csharp
public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Result<Guid>>
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<ProductImage, Guid> _imageRepository;
    private readonly IRepository<ProductVariant, Guid> _variantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISlugService _slugService;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IRepository<Product, Guid> productRepository,
        IRepository<ProductImage, Guid> imageRepository,
        IRepository<ProductVariant, Guid> variantRepository,
        IUnitOfWork unitOfWork,
        ISlugService slugService,
        ILogger<CreateProductCommandHandler> logger)
    {
        _productRepository = productRepository;
        _imageRepository = imageRepository;
        _variantRepository = variantRepository;
        _unitOfWork = unitOfWork;
        _slugService = slugService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        // Check for duplicate SKU
        var existingProduct = await _productRepository.FindAsync(
            p => p.Sku == request.Sku, cancellationToken);

        if (existingProduct.Any())
        {
            return Result<Guid>.Failure($"المنتج برمز SKU '{request.Sku}' موجود مسبقاً");
        }

        // Generate slug
        var slug = await _slugService.GenerateUniqueSlugAsync<Product>(
            request.Name, cancellationToken);

        // Create product
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = slug,
            Sku = request.Sku,
            ShortDescription = request.ShortDescription,
            LongDescription = request.LongDescription,
            BasePrice = request.BasePrice,
            CompareAtPrice = request.CompareAtPrice,
            CurrencyId = request.CurrencyId,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            StockQuantity = request.StockQuantity,
            TrackInventory = request.TrackInventory,
            Status = request.Status,
            IsActive = request.Status == ProductStatus.Active
        };

        await _productRepository.AddAsync(product, cancellationToken);

        // Add images
        if (request.Images?.Any() == true)
        {
            var displayOrder = 0;
            foreach (var imageDto in request.Images)
            {
                var image = new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Url = imageDto.Url,
                    AltText = imageDto.AltText,
                    IsPrimary = imageDto.IsPrimary,
                    DisplayOrder = imageDto.DisplayOrder > 0 ? imageDto.DisplayOrder : displayOrder++
                };
                await _imageRepository.AddAsync(image, cancellationToken);
            }
        }

        // Add variants
        if (request.Variants?.Any() == true)
        {
            var variantOrder = 0;
            foreach (var variantDto in request.Variants)
            {
                var variant = new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Title = variantDto.Title,
                    Sku = variantDto.Sku ?? $"{request.Sku}-{variantOrder + 1}",
                    Options = variantDto.Options ?? new Dictionary<string, string>(),
                    Price = variantDto.Price,
                    StockQuantity = variantDto.StockQuantity,
                    DisplayOrder = variantOrder++
                };
                await _variantRepository.AddAsync(variant, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Product created: {ProductId} ({ProductName})",
            product.Id, product.Name);

        return Result<Guid>.Success(product.Id);
    }
}
```

### UpdateProductCommand

```csharp
public record UpdateProductCommand(
    Guid Id,
    string Name,
    string Sku,
    decimal BasePrice,
    string? ShortDescription = null,
    string? LongDescription = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    decimal? CompareAtPrice = null,
    int? StockQuantity = null,
    ProductStatus? Status = null,
    bool? IsFeatured = null
) : ICommand<Result>;
```

### UpdateInventoryCommand

```csharp
public record UpdateInventoryCommand(
    Guid ProductId,
    int QuantityChange,
    InventoryChangeReason Reason,
    string? Notes = null,
    Guid? VariantId = null
) : ICommand<Result>;

public enum InventoryChangeReason
{
    Purchase = 1,
    Sale = 2,
    Return = 3,
    Adjustment = 4,
    Damage = 5,
    Restock = 6,
    Reserved = 7,
    Unreserved = 8
}
```

### BulkUpdatePricesCommand

```csharp
public record BulkUpdatePricesCommand(
    List<ProductPriceUpdate> Updates
) : ICommand<Result<BulkUpdateResult>>;

public record ProductPriceUpdate(
    Guid ProductId,
    decimal NewPrice,
    decimal? NewCompareAtPrice = null
);

public class BulkUpdateResult
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkUpdateError> Errors { get; set; } = new();
}

public record BulkUpdateError(Guid ProductId, string Error);
```

---

## الاستعلامات | Queries

### GetProductByIdQuery

```csharp
public record GetProductByIdQuery(
    Guid Id,
    bool IncludeVariants = true,
    bool IncludeImages = true,
    bool IncludeAttributes = true
) : IQuery<ProductDetailDto?>;

public class ProductDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }

    // Pricing
    public decimal BasePrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal? DiscountPercent { get; set; }

    // Category & Brand
    public CategoryDto? Category { get; set; }
    public BrandDto? Brand { get; set; }

    // Inventory
    public int StockQuantity { get; set; }
    public bool InStock { get; set; }
    public string StockStatus { get; set; } = string.Empty;

    // Media
    public List<ProductImageDto> Images { get; set; } = new();
    public string? PrimaryImageUrl { get; set; }

    // Variants
    public List<ProductVariantDto> Variants { get; set; } = new();
    public bool HasVariants { get; set; }

    // Attributes
    public List<ProductAttributeDto> Attributes { get; set; } = new();

    // Status
    public ProductStatus Status { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }

    // SEO
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

### SearchProductsQuery

```csharp
public record SearchProductsQuery(
    string? SearchTerm = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool? InStock = null,
    bool? IsFeatured = null,
    ProductStatus? Status = null,
    Dictionary<Guid, List<string>>? AttributeFilters = null,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 20
) : IQuery<SmartSearchResult<ProductListDto>>;

public class ProductListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
    public string? CategoryName { get; set; }
    public string? BrandName { get; set; }
    public int StockQuantity { get; set; }
    public bool InStock { get; set; }
    public ProductStatus Status { get; set; }
    public bool IsFeatured { get; set; }
    public double? Rating { get; set; }
    public int ReviewCount { get; set; }
}
```

### GetProductsByCategoryQuery

```csharp
public record GetProductsByCategoryQuery(
    Guid CategoryId,
    bool IncludeSubcategories = true,
    int Page = 1,
    int PageSize = 20
) : IQuery<SmartSearchResult<ProductListDto>>;
```

### GetLowStockProductsQuery

```csharp
public record GetLowStockProductsQuery(
    int? ThresholdOverride = null,
    int Page = 1,
    int PageSize = 50
) : IQuery<SmartSearchResult<LowStockProductDto>>;

public class LowStockProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int LowStockThreshold { get; set; }
    public int ReorderSuggestion { get; set; }
}
```

---

## خدمات المنتجات | Product Services

### IProductService

```csharp
public interface IProductService
{
    Task<Result<ProductDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ProductDetailDto>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<SmartSearchResult<ProductListDto>> SearchAsync(SearchProductsQuery query, CancellationToken cancellationToken = default);
    Task<Result<Guid>> CreateAsync(CreateProductCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(UpdateProductCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> UpdateInventoryAsync(UpdateInventoryCommand command, CancellationToken cancellationToken = default);
    Task<Result<List<ProductListDto>>> GetRelatedProductsAsync(Guid productId, int count = 6, CancellationToken cancellationToken = default);
}
```

### ISlugService

```csharp
public interface ISlugService
{
    string GenerateSlug(string text);
    Task<string> GenerateUniqueSlugAsync<TEntity>(string text, CancellationToken cancellationToken = default)
        where TEntity : class;
}

public class SlugService : ISlugService
{
    private readonly IServiceProvider _serviceProvider;

    public SlugService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Transliterate Arabic to English (simplified)
        var slug = text.ToLowerInvariant();

        // Remove special characters
        slug = Regex.Replace(slug, @"[^a-z0-9\s\u0600-\u06FF-]", "");

        // Replace spaces with hyphens
        slug = Regex.Replace(slug, @"\s+", "-");

        // Remove consecutive hyphens
        slug = Regex.Replace(slug, @"-+", "-");

        // Trim hyphens from ends
        slug = slug.Trim('-');

        return slug;
    }

    public async Task<string> GenerateUniqueSlugAsync<TEntity>(
        string text,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var baseSlug = GenerateSlug(text);
        var slug = baseSlug;
        var counter = 1;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();

        while (await context.Set<TEntity>()
            .Where(e => EF.Property<string>(e, "Slug") == slug)
            .AnyAsync(cancellationToken))
        {
            slug = $"{baseSlug}-{counter++}";
        }

        return slug;
    }
}
```

---

## تسجيل الخدمات | Service Registration

```csharp
public static class ProductsServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogProducts(
        this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IRepository<Product, Guid>, EfCoreRepository<Product, Guid>>();
        services.AddScoped<IRepository<ProductVariant, Guid>, EfCoreRepository<ProductVariant, Guid>>();
        services.AddScoped<IRepository<ProductImage, Guid>, EfCoreRepository<ProductImage, Guid>>();
        services.AddScoped<IRepository<ProductAttribute, Guid>, EfCoreRepository<ProductAttribute, Guid>>();

        // Register services
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ISlugService, SlugService>();

        // Register CQRS
        services.AddCqrs(typeof(CreateProductCommand).Assembly);

        return services;
    }
}
```

---

## أفضل الممارسات | Best Practices

### 1. استخدام Variants للمنتجات المتنوعة
```csharp
// ✅ صحيح - استخدام Variants
var product = new Product { Name = "قميص" };
var variants = new[]
{
    new ProductVariant { Title = "قميص - أحمر - S", Options = { ["اللون"] = "أحمر", ["المقاس"] = "S" } },
    new ProductVariant { Title = "قميص - أحمر - M", Options = { ["اللون"] = "أحمر", ["المقاس"] = "M" } },
    new ProductVariant { Title = "قميص - أزرق - S", Options = { ["اللون"] = "أزرق", ["المقاس"] = "S" } },
};

// ❌ خاطئ - إنشاء منتجات منفصلة
var product1 = new Product { Name = "قميص أحمر - S" };
var product2 = new Product { Name = "قميص أحمر - M" };
```

### 2. تتبع المخزون
```csharp
// استخدام UpdateInventoryCommand للحفاظ على سجل التغييرات
await _mediator.Send(new UpdateInventoryCommand(
    ProductId: productId,
    QuantityChange: -1,
    Reason: InventoryChangeReason.Sale,
    Notes: $"Order #{orderId}"
));
```

### 3. استخدام Slugs للـ SEO
```csharp
// URL صديق لمحركات البحث
// /products/smart-watch-pro-max بدلاً من /products/a1b2c3d4-...
```

---

## المراجع | References

- [E-commerce Product Data Models](https://www.shopify.com/partners/blog/product-api)
- [Inventory Management Best Practices](https://www.netsuite.com/portal/resource/articles/inventory-management/inventory-management-best-practices.shtml)
