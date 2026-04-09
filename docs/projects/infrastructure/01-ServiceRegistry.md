# ACommerce Service Registry

## نظرة عامة
نظام Service Registry للاكتشاف الديناميكي للخدمات (Service Discovery). يتكون من 4 مكتبات: Abstractions, Core, Client, Server.

## المكتبات

| المكتبة | الموقع | الوصف |
|---------|--------|-------|
| `ServiceRegistry.Abstractions` | `/Infrastructure/ACommerce.ServiceRegistry.Abstractions` | التجريدات والواجهات |
| `ServiceRegistry.Core` | `/Infrastructure/ACommerce.ServiceRegistry.Core` | المنطق الأساسي |
| `ServiceRegistry.Client` | `/Infrastructure/ACommerce.ServiceRegistry.Client` | Client SDK للخدمات |
| `ServiceRegistry.Server` | `/Infrastructure/ACommerce.ServiceRegistry.Server` | خادم Registry (ASP.NET Core) |

---

## ServiceRegistry.Abstractions

### الواجهات

#### IServiceRegistry
إدارة تسجيل الخدمات:

```csharp
public interface IServiceRegistry
{
    Task<ServiceEndpoint> RegisterAsync(
        ServiceRegistration registration,
        CancellationToken cancellationToken = default);

    Task<bool> DeregisterAsync(
        string serviceId,
        CancellationToken cancellationToken = default);

    Task UpdateHealthAsync(
        string serviceId,
        ServiceHealth health,
        CancellationToken cancellationToken = default);

    Task<ServiceEndpoint?> GetByIdAsync(
        string serviceId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ServiceEndpoint>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task HeartbeatAsync(
        string serviceId,
        CancellationToken cancellationToken = default);
}
```

#### IServiceDiscovery
اكتشاف الخدمات:

```csharp
public interface IServiceDiscovery
{
    Task<ServiceEndpoint?> DiscoverAsync(
        ServiceQuery query,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ServiceEndpoint>> DiscoverAllAsync(
        ServiceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceEndpoint?> GetServiceAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ServiceEndpoint>> GetAllInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default);
}
```

### النماذج

#### ServiceEndpoint
معلومات نقطة النهاية:

```csharp
public sealed class ServiceEndpoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ServiceName { get; set; }          // Products, Orders, etc.
    public string Version { get; set; } = "v1";
    public string BaseUrl { get; set; }
    public string Environment { get; set; } = "Development";
    public int Weight { get; set; } = 100;           // للـ Load Balancing
    public Dictionary<string, string> Tags { get; set; }
    public ServiceHealth Health { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime RegisteredAt { get; set; }
    public bool IsActive => Health.Status == HealthStatus.Healthy;
}
```

#### ServiceRegistration
طلب تسجيل خدمة:

```csharp
public sealed class ServiceRegistration
{
    public string ServiceName { get; set; }
    public string Version { get; set; } = "v1";
    public string BaseUrl { get; set; }
    public string Environment { get; set; }
    public int Weight { get; set; } = 100;
    public Dictionary<string, string> Tags { get; set; }
}
```

---

## ServiceRegistry.Client

### ServiceRegistryClient
Client للتواصل مع Registry:

```csharp
public sealed class ServiceRegistryClient
{
    // تسجيل خدمة
    public async Task<ServiceEndpoint?> RegisterAsync(
        ServiceRegistration registration,
        CancellationToken cancellationToken = default);

    // إلغاء تسجيل خدمة
    public async Task<bool> DeregisterAsync(
        string serviceId,
        CancellationToken cancellationToken = default);

    // اكتشاف خدمة (مع Cache)
    public async Task<ServiceEndpoint?> DiscoverAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    // الحصول على جميع نسخ خدمة
    public async Task<IEnumerable<ServiceEndpoint>> GetAllInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    // إرسال Heartbeat
    public async Task<bool> SendHeartbeatAsync(
        string serviceId,
        CancellationToken cancellationToken = default);

    // مسح Cache
    public void ClearCache();
}
```

### ميزات Client
- **Caching**: تخزين مؤقت لنتائج الاكتشاف
- **Stale Cache**: استخدام Cache القديم عند الفشل
- **Auto Registration**: تسجيل تلقائي عند بدء الخدمة
- **Heartbeat**: إرسال نبضات القلب دورياً

---

## ServiceRegistry.Server

### نقاط النهاية

#### Registry API
```
POST   /api/registry/register     - تسجيل خدمة
DELETE /api/registry/{serviceId}  - إلغاء تسجيل
POST   /api/registry/{id}/heartbeat - Heartbeat
GET    /api/registry              - جميع الخدمات
```

#### Discovery API
```
GET    /api/discovery/{serviceName}           - اكتشاف خدمة
GET    /api/discovery/{serviceName}/instances - جميع نسخ الخدمة
POST   /api/discovery/discover                - اكتشاف متقدم
```

---

## مثال استخدام

### تسجيل خدمة
```csharp
var registration = new ServiceRegistration
{
    ServiceName = "Products",
    Version = "v1",
    BaseUrl = "https://products.example.com",
    Environment = "Production",
    Tags = { ["region"] = "sa-east" }
};

var endpoint = await registryClient.RegisterAsync(registration);
```

### اكتشاف خدمة
```csharp
var endpoint = await registryClient.DiscoverAsync("Products");
var url = $"{endpoint.BaseUrl}/api/products";
```

### تسجيل تلقائي في Startup
```csharp
services.AddServiceRegistryClient(options =>
{
    options.RegistryUrl = "https://registry.example.com";
    options.AutoRegister = true;
    options.HeartbeatInterval = TimeSpan.FromSeconds(30);
});
```

---

## بنية الملفات
```
Infrastructure/
├── ACommerce.ServiceRegistry.Abstractions/
│   ├── Interfaces/
│   │   ├── IServiceRegistry.cs
│   │   ├── IServiceDiscovery.cs
│   │   ├── IServiceStore.cs
│   │   └── IHealthChecker.cs
│   └── Models/
│       ├── ServiceEndpoint.cs
│       ├── ServiceRegistration.cs
│       ├── ServiceQuery.cs
│       └── ServiceHealth.cs
├── ACommerce.ServiceRegistry.Core/
│   ├── Services/
│   │   ├── ServiceRegistry.cs
│   │   ├── ServiceDiscovery.cs
│   │   └── HealthChecker.cs
│   └── Storage/
│       └── InMemoryServiceStore.cs
├── ACommerce.ServiceRegistry.Client/
│   ├── ServiceRegistryClient.cs
│   ├── Cache/
│   │   └── ServiceCache.cs
│   └── Services/
│       └── ServiceRegistrationHostedService.cs
└── ACommerce.ServiceRegistry.Server/
    ├── Controllers/
    │   ├── RegistryController.cs
    │   └── DiscoveryController.cs
    └── Services/
        └── HealthCheckBackgroundService.cs
```

---

## ملاحظات تقنية

1. **Service Discovery**: اكتشاف ديناميكي للخدمات
2. **Load Balancing**: توزيع الحمل بناءً على Weight
3. **Health Checking**: فحص صحة الخدمات دورياً
4. **Caching**: تخزين مؤقت مع Stale Support
5. **Heartbeat**: نبضات قلب لتأكيد حياة الخدمة
6. **Multi-Environment**: دعم بيئات متعددة
