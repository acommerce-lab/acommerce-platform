# ACommerce Client SDKs

## نظرة عامة
مجموعة من Client SDKs للتعامل مع خدمات ACommerce من التطبيقات الأمامية. جميعها تستخدم نفس النمط وتعتمد على `ACommerce.Client.Core`.

---

## Client SDKs المتاحة

| Client | الوصف | Service Name |
|--------|-------|--------------|
| `ACommerce.Client.Auth` | المصادقة وتسجيل الدخول | Auth |
| `ACommerce.Client.Cart` | سلة التسوق | Sales |
| `ACommerce.Client.Categories` | التصنيفات | Marketplace |
| `ACommerce.Client.Chats` | المحادثات | Chats |
| `ACommerce.Client.ContactPoints` | نقاط الاتصال | Identity |
| `ACommerce.Client.Files` | الملفات والصور | Files |
| `ACommerce.Client.Notifications` | الإشعارات | Notifications |
| `ACommerce.Client.Orders` | الطلبات | Sales |
| `ACommerce.Client.Payments` | المدفوعات | Payments |
| `ACommerce.Client.ProductListings` | عروض المنتجات | Marketplace |
| `ACommerce.Client.Products` | المنتجات | Marketplace |
| `ACommerce.Client.Profiles` | الملفات الشخصية | Identity |
| `ACommerce.Client.Realtime` | الاتصال اللحظي | Realtime |
| `ACommerce.Client.Shipping` | الشحن | Shipping |
| `ACommerce.Client.Vendors` | البائعين | Marketplace |

---

## نمط Client SDK

جميع Clients تتبع نفس النمط:

```csharp
public sealed class XxxClient
{
    private readonly IApiClient _httpClient;
    private const string ServiceName = "ServiceName";
    private const string BasePath = "/api/xxx";

    public XxxClient(IApiClient httpClient)
    {
        _httpClient = httpClient;
    }

    // عمليات CRUD
    public async Task<XxxDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _httpClient.GetAsync<XxxDto>(ServiceName, $"{BasePath}/{id}", ct);

    public async Task<List<XxxDto>?> GetAllAsync(CancellationToken ct = default)
        => await SearchAsync(new SearchRequest(), ct).Items;

    public async Task<XxxDto?> CreateAsync(CreateXxxRequest request, CancellationToken ct = default)
        => await _httpClient.PostAsync<CreateXxxRequest, XxxDto>(ServiceName, BasePath, request, ct);

    public async Task<XxxDto?> UpdateAsync(Guid id, UpdateXxxRequest request, CancellationToken ct = default)
        => await _httpClient.PutAsync<UpdateXxxRequest, XxxDto>(ServiceName, $"{BasePath}/{id}", request, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        => await _httpClient.DeleteAsync(ServiceName, $"{BasePath}/{id}", ct);

    // البحث
    public async Task<PagedResult<XxxDto>?> SearchAsync(SearchRequest request, CancellationToken ct = default)
        => await _httpClient.PostAsync<SearchRequest, PagedResult<XxxDto>>(ServiceName, $"{BasePath}/search", request, ct);
}
```

---

## مثال: ProductsClient

```csharp
public sealed class ProductsClient
{
    private const string ServiceName = "Marketplace";
    private const string BasePath = "/api/catalog/products";

    // البحث في المنتجات (SmartSearch)
    public async Task<PagedProductResult?> SearchAsync(ProductSearchRequest? request = null, CancellationToken ct = default);

    // الحصول على منتج محدد
    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // المنتجات المميزة
    public async Task<List<ProductDto>?> GetFeaturedAsync(int limit = 10, CancellationToken ct = default);

    // المنتجات الجديدة
    public async Task<List<ProductDto>?> GetNewAsync(int limit = 10, CancellationToken ct = default);

    // إنشاء منتج
    public async Task<ProductDto?> CreateAsync(CreateProductRequest request, CancellationToken ct = default);

    // تحديث منتج
    public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);

    // حذف منتج
    public async Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

---

## DTOs المشتركة

### PagedResult<T>
```csharp
public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
```

### SearchRequest
```csharp
public sealed class SearchRequest
{
    public string? SearchTerm { get; set; }
    public List<FilterItem>? Filters { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? OrderBy { get; set; }
    public bool Ascending { get; set; } = true;
    public List<string>? IncludeProperties { get; set; }
    public bool IncludeDeleted { get; set; }
}
```

### FilterItem
```csharp
public sealed class FilterItem
{
    public string PropertyName { get; set; }
    public object? Value { get; set; }
    public object? SecondValue { get; set; }
    public int Operator { get; set; }
}
```

---

## تسجيل الخدمات

### تسجيل جميع Clients
```csharp
services.AddACommerceClients(options =>
{
    options.ServiceRegistryUrl = "https://registry.example.com";
});

// أو تسجيل clients محددة
services.AddScoped<ProductsClient>();
services.AddScoped<CartClient>();
services.AddScoped<OrdersClient>();
```

---

## مثال استخدام في Blazor

```razor
@inject ProductsClient ProductsClient

@code {
    private List<ProductDto>? products;

    protected override async Task OnInitializedAsync()
    {
        var result = await ProductsClient.SearchAsync(new ProductSearchRequest
        {
            PageSize = 10,
            OrderBy = "CreatedAt",
            Ascending = false
        });

        products = result?.Items;
    }
}
```

---

## ملاحظات تقنية

1. **Unified Pattern**: جميع Clients تتبع نفس النمط
2. **Service Discovery**: تستخدم Service Registry للاكتشاف
3. **Type Safety**: DTOs مخصصة لكل client
4. **SmartSearch**: دعم البحث المتقدم
5. **Pagination**: دعم التصفح لجميع القوائم
6. **Cancellation**: دعم CancellationToken
