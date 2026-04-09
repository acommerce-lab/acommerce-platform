# ACommerce.Locations

## معلومات المشروع | Project Info

| الخاصية | القيمة |
|---------|--------|
| **المسار** | `Other/ACommerce.Locations` |
| **النوع** | Class Library |
| **الإطار** | .NET 9.0 |
| **الاعتماديات** | `ACommerce.Locations.Abstractions`, `Microsoft.EntityFrameworkCore` |

## الوصف | Description

مكتبة التنفيذ لنظام المواقع الجغرافية. تتضمن إعدادات Entity Framework Core، تنفيذ الخدمات، وبيانات البذر الأولية للمملكة العربية السعودية.

---

## الهيكل | Structure

```
ACommerce.Locations/
├── Configurations/
│   ├── CountryConfiguration.cs
│   ├── RegionConfiguration.cs
│   ├── CityConfiguration.cs
│   ├── NeighborhoodConfiguration.cs
│   └── AddressConfiguration.cs
├── Services/
│   ├── LocationService.cs
│   └── GeoService.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── ModelBuilderExtensions.cs
└── Seed/
    └── SaudiArabiaSeed.cs
```

---

## التكوين والإعداد | Configuration

### 1. تسجيل الخدمات

```csharp
// في Program.cs أو Startup.cs
builder.Services.AddACommerceLocations();
```

### 2. إعداد DbContext

```csharp
public class AppDbContext : DbContext
{
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();
    public DbSet<Address> Addresses => Set<Address>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // تطبيق إعدادات المواقع
        modelBuilder.ApplyLocationsConfigurations();

        // إضافة بيانات السعودية (اختياري)
        SaudiArabiaSeed.Seed(modelBuilder);
    }
}
```

---

## إعدادات EF Core | EF Core Configurations

### CountryConfiguration

```csharp
public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries", "locations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(100);
        builder.Property(x => x.Code).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Code3).HasMaxLength(3);
        builder.Property(x => x.PhoneCode).HasMaxLength(10);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3);
        builder.Property(x => x.CurrencyName).HasMaxLength(50);
        builder.Property(x => x.CurrencySymbol).HasMaxLength(10);
        builder.Property(x => x.Flag).HasMaxLength(50);
        builder.Property(x => x.Timezone).HasMaxLength(50);

        // الفهارس
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsDeleted);

        // Query Filter للحذف المنطقي
        builder.HasQueryFilter(x => !x.IsDeleted);

        // العلاقات
        builder.HasMany(x => x.Regions)
            .WithOne(x => x.Country)
            .HasForeignKey(x => x.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### RegionConfiguration

```csharp
public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions", "locations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(100);
        builder.Property(x => x.Code).HasMaxLength(20);

        // الفهارس
        builder.HasIndex(x => x.CountryId);
        builder.HasIndex(x => new { x.CountryId, x.Code }).IsUnique()
            .HasFilter("[Code] IS NOT NULL");

        builder.HasQueryFilter(x => !x.IsDeleted);

        // العلاقات
        builder.HasMany(x => x.Cities)
            .WithOne(x => x.Region)
            .HasForeignKey(x => x.RegionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

## الخدمات | Services

### LocationService

التنفيذ الكامل لـ `ILocationService`:

```csharp
public class LocationService : ILocationService
{
    private readonly DbContext _context;

    public LocationService(DbContext context)
    {
        _context = context;
    }

    public async Task<List<CountryResponseDto>> GetCountriesAsync(
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        var query = _context.Set<Country>().AsQueryable();

        if (activeOnly)
            query = query.Where(x => x.IsActive);

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CountryResponseDto
            {
                Id = x.Id,
                Name = x.Name,
                NameEn = x.NameEn,
                Code = x.Code,
                PhoneCode = x.PhoneCode,
                Flag = x.Flag,
                IsActive = x.IsActive
            })
            .ToListAsync(ct);
    }

    // ... باقي التنفيذ
}
```

### GeoService

خدمة البحث الجغرافي مع حساب المسافة باستخدام صيغة Haversine:

```csharp
public class GeoService : IGeoService
{
    private readonly DbContext _context;
    private const double EarthRadiusKm = 6371;

    public GeoService(DbContext context)
    {
        _context = context;
    }

    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    public async Task<List<GeoSearchResult<CityResponseDto>>> FindNearbyCitiesAsync(
        GeoSearchRequest request,
        CancellationToken ct = default)
    {
        var cities = await _context.Set<City>()
            .Where(x => x.IsActive && x.Latitude != null && x.Longitude != null)
            .Select(x => new
            {
                City = x,
                Distance = CalculateDistance(
                    request.Latitude, request.Longitude,
                    x.Latitude!.Value, x.Longitude!.Value)
            })
            .Where(x => x.Distance <= request.RadiusKm)
            .OrderBy(x => x.Distance)
            .Take(request.Limit)
            .ToListAsync(ct);

        return cities.Select(x => new GeoSearchResult<CityResponseDto>
        {
            Item = MapToDto(x.City),
            DistanceKm = x.Distance
        }).ToList();
    }

    public async Task<LocationHierarchyDto?> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default)
    {
        // البحث عن أقرب حي
        var nearestNeighborhood = await _context.Set<Neighborhood>()
            .Include(x => x.City)
                .ThenInclude(x => x!.Region)
                    .ThenInclude(x => x!.Country)
            .Where(x => x.Latitude != null && x.Longitude != null)
            .OrderBy(x => /* Distance calculation */)
            .FirstOrDefaultAsync(ct);

        if (nearestNeighborhood == null) return null;

        return new LocationHierarchyDto
        {
            Country = MapToDto(nearestNeighborhood.City!.Region!.Country!),
            Region = MapToDto(nearestNeighborhood.City!.Region!),
            City = MapToDto(nearestNeighborhood.City!),
            Neighborhood = MapToDto(nearestNeighborhood)
        };
    }
}
```

---

## بيانات البذر | Seed Data

### SaudiArabiaSeed

بيانات كاملة للمملكة العربية السعودية:

```csharp
public static class SaudiArabiaSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        // الدولة
        var saudiArabia = new Country
        {
            Id = Guid.Parse("..."),
            Name = "المملكة العربية السعودية",
            NameEn = "Saudi Arabia",
            Code = "SA",
            Code3 = "SAU",
            NumericCode = 682,
            PhoneCode = "+966",
            CurrencyCode = "SAR",
            CurrencyName = "ريال سعودي",
            CurrencySymbol = "ر.س",
            Latitude = 23.8859,
            Longitude = 45.0792,
            Timezone = "Asia/Riyadh"
        };

        modelBuilder.Entity<Country>().HasData(saudiArabia);

        // المناطق (13 منطقة إدارية)
        var regions = new[]
        {
            CreateRegion("منطقة الرياض", "Riyadh Region", "RUH"),
            CreateRegion("منطقة مكة المكرمة", "Makkah Region", "MKH"),
            CreateRegion("المنطقة الشرقية", "Eastern Province", "EST"),
            CreateRegion("منطقة المدينة المنورة", "Madinah Region", "MED"),
            // ... باقي المناطق
        };

        modelBuilder.Entity<Region>().HasData(regions);

        // المدن الرئيسية (10 مدن)
        var cities = new[]
        {
            CreateCity("الرياض", "Riyadh", riyadhRegionId, 7500000, isCapital: true),
            CreateCity("جدة", "Jeddah", makkahRegionId, 4500000),
            CreateCity("مكة المكرمة", "Makkah", makkahRegionId, 2000000),
            CreateCity("المدينة المنورة", "Madinah", madinahRegionId, 1500000),
            CreateCity("الدمام", "Dammam", easternRegionId, 1200000),
            // ... باقي المدن
        };

        modelBuilder.Entity<City>().HasData(cities);

        // أحياء الرياض (15 حي)
        var riyadhNeighborhoods = new[]
        {
            CreateNeighborhood("العليا", "Al Olaya", riyadhCityId, "12211"),
            CreateNeighborhood("السليمانية", "Al Sulaimaniyah", riyadhCityId, "12221"),
            CreateNeighborhood("الملز", "Al Malaz", riyadhCityId, "12836"),
            CreateNeighborhood("النزهة", "Al Nuzha", riyadhCityId, "12471"),
            CreateNeighborhood("الورود", "Al Wurud", riyadhCityId, "12252"),
            // ... باقي الأحياء
        };

        modelBuilder.Entity<Neighborhood>().HasData(riyadhNeighborhoods);
    }
}
```

### البيانات المتوفرة

| النوع | العدد | التفاصيل |
|-------|-------|----------|
| الدول | 1 | المملكة العربية السعودية |
| المناطق | 13 | جميع المناطق الإدارية |
| المدن | 10 | الرياض، جدة، مكة، المدينة، الدمام، الخبر، الظهران، أبها، تبوك، بريدة |
| الأحياء | 15 | أحياء مدينة الرياض الرئيسية |

---

## Extensions

### ServiceCollectionExtensions

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddACommerceLocations(this IServiceCollection services)
    {
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IGeoService, GeoService>();

        return services;
    }
}
```

### ModelBuilderExtensions

```csharp
public static class ModelBuilderExtensions
{
    public static ModelBuilder ApplyLocationsConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CountryConfiguration());
        modelBuilder.ApplyConfiguration(new RegionConfiguration());
        modelBuilder.ApplyConfiguration(new CityConfiguration());
        modelBuilder.ApplyConfiguration(new NeighborhoodConfiguration());
        modelBuilder.ApplyConfiguration(new AddressConfiguration());

        return modelBuilder;
    }
}
```

---

## الفهارس المنشأة | Created Indexes

| الجدول | الفهرس | النوع |
|--------|--------|-------|
| Countries | `Code` | Unique |
| Countries | `IsActive`, `IsDeleted` | Non-Unique |
| Regions | `CountryId` | Non-Unique |
| Regions | `CountryId, Code` | Unique (Filtered) |
| Cities | `RegionId` | Non-Unique |
| Cities | `RegionId, Code` | Unique (Filtered) |
| Neighborhoods | `CityId` | Non-Unique |
| Neighborhoods | `PostalCode` | Non-Unique |
| Addresses | `EntityType, EntityId` | Non-Unique |
| Addresses | `CountryId`, `RegionId`, `CityId`, `NeighborhoodId` | Non-Unique |

---

## مثال كامل | Full Example

```csharp
// إعداد الخدمات
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddACommerceLocations();

// استخدام الخدمة
public class LocationController : ControllerBase
{
    private readonly ILocationService _locationService;
    private readonly IGeoService _geoService;

    public LocationController(
        ILocationService locationService,
        IGeoService geoService)
    {
        _locationService = locationService;
        _geoService = geoService;
    }

    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries()
    {
        var countries = await _locationService.GetCountriesAsync();
        return Ok(countries);
    }

    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearbyCities(
        double lat, double lon, double radius = 50)
    {
        var cities = await _geoService.FindNearbyCitiesAsync(new GeoSearchRequest
        {
            Latitude = lat,
            Longitude = lon,
            RadiusKm = radius
        });
        return Ok(cities);
    }
}
```
