# ACommerce.Client.Core

## نظرة عامة
البنية الأساسية لجميع Client SDKs. توفر واجهة موحدة للتعامل مع HTTP APIs مع دعم Service Discovery و Authentication و Retry و Localization.

## الموقع
`/Clients/ACommerce.Client.Core`

## التبعيات
- `ACommerce.ServiceRegistry.Client`
- `System.Net.Http.Json`
- `Microsoft.Extensions.Logging`

---

## الواجهات (Contracts)

### IApiClient
واجهة موحدة للتعامل مع HTTP APIs:

```csharp
public interface IApiClient
{
    Task<T?> GetAsync<T>(
        string serviceName,
        string path,
        CancellationToken cancellationToken = default);

    Task<TResponse?> PostAsync<TRequest, TResponse>(
        string serviceName,
        string path,
        TRequest data,
        CancellationToken cancellationToken = default);

    Task PostAsync<TRequest>(
        string serviceName,
        string path,
        TRequest data,
        CancellationToken cancellationToken = default);

    Task<TResponse?> PutAsync<TRequest, TResponse>(
        string serviceName,
        string path,
        TRequest data,
        CancellationToken cancellationToken = default);

    Task PutAsync<TRequest>(
        string serviceName,
        string path,
        TRequest data,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string serviceName,
        string path,
        CancellationToken cancellationToken = default);

    Task<TResponse?> PatchAsync<TRequest, TResponse>(
        string serviceName,
        string path,
        TRequest data,
        CancellationToken cancellationToken = default);
}
```

### ITokenProvider
واجهة للحصول على Authentication Token:

```csharp
public interface ITokenProvider
{
    Task<string?> GetTokenAsync();
}
```

---

## الخدمات (Services)

### DynamicHttpClient
HTTP Client مع Dynamic Service URLs:

```csharp
public sealed class DynamicHttpClient : IApiClient
```

يستخدم Service Registry للحصول على URLs ديناميكياً:

```csharp
private async Task<string> BuildUrlAsync(
    string serviceName,
    string path,
    CancellationToken cancellationToken)
{
    var endpoint = await _registryClient.DiscoverAsync(serviceName, cancellationToken);

    if (endpoint == null)
    {
        throw new InvalidOperationException($"Service not found: {serviceName}");
    }

    var baseUrl = endpoint.BaseUrl.TrimEnd('/');
    var cleanPath = path.StartsWith('/') ? path : $"/{path}";

    return $"{baseUrl}{cleanPath}";
}
```

### StaticHttpClient
HTTP Client مع Static URLs (للتكوين الثابت).

---

## Interceptors

### AuthenticationInterceptor
إضافة Authentication Token تلقائياً:

```csharp
public sealed class AuthenticationInterceptor : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

### RetryInterceptor
إعادة المحاولة عند الفشل.

### LocalizationInterceptor
إضافة Accept-Language Header.

---

## بنية الملفات
```
ACommerce.Client.Core/
├── Http/
│   ├── IApiClient.cs
│   ├── DynamicHttpClient.cs
│   └── StaticHttpClient.cs
├── Interceptors/
│   ├── AuthenticationInterceptor.cs
│   ├── RetryInterceptor.cs
│   └── LocalizationInterceptor.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

---

## مثال استخدام

### تسجيل الخدمات
```csharp
services.AddACommerceClients(options =>
{
    options.ServiceRegistryUrl = "https://registry.example.com";
    options.DefaultTimeout = TimeSpan.FromSeconds(30);
});
```

### استخدام في Client
```csharp
public sealed class ProductsClient
{
    private readonly IApiClient _httpClient;
    private const string ServiceName = "Marketplace";
    private const string BasePath = "/api/catalog/products";

    public ProductsClient(IApiClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _httpClient.GetAsync<ProductDto>(ServiceName, $"{BasePath}/{id}", ct);
    }
}
```

---

## ملاحظات تقنية

1. **Service Discovery**: اكتشاف الخدمات ديناميكياً
2. **DelegatingHandler**: استخدام Interceptors للـ cross-cutting concerns
3. **JSON Serialization**: CamelCase naming policy
4. **Error Handling**: Logging متكامل للأخطاء
5. **Typed Clients**: دعم الـ strongly-typed clients
