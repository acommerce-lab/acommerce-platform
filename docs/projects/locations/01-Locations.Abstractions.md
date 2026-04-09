# ACommerce.Locations.Abstractions

## معلومات المشروع | Project Info

| الخاصية | القيمة |
|---------|--------|
| **المسار** | `Core/ACommerce.Locations.Abstractions` |
| **النوع** | Class Library |
| **الإطار** | .NET 9.0 |
| **الاعتماديات** | `ACommerce.SharedKernel.Abstractions` |

## الوصف | Description

مكتبة التجريدات الأساسية لإدارة التسلسل الهرمي الجغرافي (الدول، المناطق/الأقاليم، المدن، الأحياء). توفر الكيانات والعقود وكائنات نقل البيانات (DTOs) اللازمة لبناء نظام مواقع جغرافية متكامل.

---

## الهيكل | Structure

```
ACommerce.Locations.Abstractions/
├── Entities/
│   ├── Country.cs          # الدولة
│   ├── Region.cs           # المنطقة/الإقليم
│   ├── City.cs             # المدينة
│   ├── Neighborhood.cs     # الحي
│   └── Address.cs          # العنوان
├── DTOs/
│   ├── CountryDto.cs       # DTOs للدول
│   ├── RegionDto.cs        # DTOs للمناطق
│   ├── CityDto.cs          # DTOs للمدن
│   ├── NeighborhoodDto.cs  # DTOs للأحياء
│   ├── AddressDto.cs       # DTOs للعناوين
│   └── GeoSearchDto.cs     # DTOs للبحث الجغرافي
└── Contracts/
    ├── ILocationService.cs  # خدمة المواقع
    ├── IAddressService.cs   # خدمة العناوين
    └── IGeoService.cs       # خدمة البحث الجغرافي
```

---

## الكيانات | Entities

### 1. Country (الدولة)

```csharp
public class Country : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // معلومات أساسية
    public required string Name { get; set; }     // الاسم (اللغة الافتراضية)
    public string? NameEn { get; set; }           // الاسم بالإنجليزية

    // رموز ISO
    public required string Code { get; set; }     // ISO 3166-1 alpha-2 (SA)
    public string? Code3 { get; set; }            // ISO 3166-1 alpha-3 (SAU)
    public int? NumericCode { get; set; }         // ISO 3166-1 numeric (682)

    // الاتصالات والعملة
    public string? PhoneCode { get; set; }        // رمز الاتصال (+966)
    public string? CurrencyCode { get; set; }     // رمز العملة (SAR)
    public string? CurrencyName { get; set; }     // اسم العملة
    public string? CurrencySymbol { get; set; }   // رمز العملة (ر.س)

    // معلومات جغرافية
    public string? Flag { get; set; }             // العلم
    public double? Latitude { get; set; }         // خط العرض
    public double? Longitude { get; set; }        // خط الطول
    public string? Timezone { get; set; }         // المنطقة الزمنية

    // الحالة والترتيب
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // العلاقات
    public List<Region> Regions { get; set; } = [];
}
```

---

### 2. Region (المنطقة/الإقليم)

```csharp
public class Region : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // معلومات أساسية
    public required string Name { get; set; }
    public string? NameEn { get; set; }
    public string? Code { get; set; }             // رمز المنطقة

    // نوع المنطقة (مرونة للدول المختلفة)
    public RegionType Type { get; set; } = RegionType.Region;

    // الموقع الجغرافي
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // الحالة والترتيب
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // العلاقات
    public Guid CountryId { get; set; }
    public Country? Country { get; set; }
    public List<City> Cities { get; set; } = [];
}

public enum RegionType
{
    Region = 1,      // منطقة (السعودية)
    Emirate = 2,     // إمارة (الإمارات)
    Governorate = 3, // محافظة (مصر، الكويت)
    State = 4,       // ولاية (أمريكا)
    Province = 5     // مقاطعة (كندا)
}
```

---

### 3. City (المدينة)

```csharp
public class City : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // معلومات أساسية
    public required string Name { get; set; }
    public string? NameEn { get; set; }
    public string? Code { get; set; }

    // معلومات إضافية
    public int? Population { get; set; }          // عدد السكان
    public bool IsCapital { get; set; }           // عاصمة المنطقة

    // الموقع الجغرافي
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // الحالة والترتيب
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // العلاقات
    public Guid RegionId { get; set; }
    public Region? Region { get; set; }
    public List<Neighborhood> Neighborhoods { get; set; } = [];
}
```

---

### 4. Neighborhood (الحي)

```csharp
public class Neighborhood : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // معلومات أساسية
    public required string Name { get; set; }
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public string? PostalCode { get; set; }       // الرمز البريدي

    // الموقع الجغرافي
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // حدود الحي (GeoJSON للخرائط)
    public string? Boundaries { get; set; }

    // الحالة والترتيب
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // العلاقات
    public Guid CityId { get; set; }
    public City? City { get; set; }
}
```

---

### 5. Address (العنوان)

كيان عام للعناوين يمكن ربطه بأي كيان آخر.

```csharp
public class Address : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // ربط بكيان خارجي (Polymorphic)
    public required string EntityType { get; set; }  // "User", "Vendor", "Order"
    public Guid EntityId { get; set; }

    // تفاصيل العنوان
    public required string Label { get; set; }       // اسم العنوان (المنزل، العمل)
    public string? Street { get; set; }              // الشارع
    public string? BuildingNumber { get; set; }      // رقم المبنى
    public string? Floor { get; set; }               // الطابق
    public string? Apartment { get; set; }           // الشقة
    public string? AdditionalInfo { get; set; }      // معلومات إضافية
    public string? PostalCode { get; set; }          // الرمز البريدي

    // الموقع
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // العلاقات
    public Guid? NeighborhoodId { get; set; }
    public Neighborhood? Neighborhood { get; set; }
    public Guid? CityId { get; set; }
    public City? City { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public Guid CountryId { get; set; }
    public Country? Country { get; set; }

    // الحالة
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
```

---

## العقود | Contracts

### ILocationService

الخدمة الرئيسية لإدارة التسلسل الهرمي للمواقع.

```csharp
public interface ILocationService
{
    // الدول
    Task<List<CountryResponseDto>> GetCountriesAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<CountryDetailDto?> GetCountryByIdAsync(Guid id, CancellationToken ct = default);
    Task<CountryResponseDto?> GetCountryByCodeAsync(string code, CancellationToken ct = default);

    // المناطق
    Task<List<RegionResponseDto>> GetRegionsByCountryAsync(Guid countryId, bool activeOnly = true, CancellationToken ct = default);
    Task<RegionDetailDto?> GetRegionByIdAsync(Guid id, CancellationToken ct = default);

    // المدن
    Task<List<CityResponseDto>> GetCitiesByRegionAsync(Guid regionId, bool activeOnly = true, CancellationToken ct = default);
    Task<List<CityResponseDto>> GetCitiesByCountryAsync(Guid countryId, bool activeOnly = true, CancellationToken ct = default);
    Task<CityDetailDto?> GetCityByIdAsync(Guid id, CancellationToken ct = default);

    // الأحياء
    Task<List<NeighborhoodResponseDto>> GetNeighborhoodsByCityAsync(Guid cityId, bool activeOnly = true, CancellationToken ct = default);
    Task<NeighborhoodDetailDto?> GetNeighborhoodByIdAsync(Guid id, CancellationToken ct = default);

    // التسلسل الهرمي والبحث
    Task<LocationHierarchyDto?> GetLocationHierarchyAsync(Guid? neighborhoodId = null, Guid? cityId = null, Guid? regionId = null, Guid? countryId = null, CancellationToken ct = default);
    Task<List<LocationSearchResult>> SearchLocationsAsync(string query, Guid? countryId = null, int limit = 20, CancellationToken ct = default);
}
```

---

### IGeoService

خدمة البحث الجغرافي المبني على الإحداثيات.

```csharp
public interface IGeoService
{
    // البحث بالقرب من نقطة
    Task<List<GeoSearchResult<CityResponseDto>>> FindNearbyCitiesAsync(GeoSearchRequest request, CancellationToken ct = default);
    Task<List<GeoSearchResult<NeighborhoodResponseDto>>> FindNearbyNeighborhoodsAsync(GeoSearchRequest request, CancellationToken ct = default);

    // تحديد الموقع العكسي
    Task<LocationHierarchyDto?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);

    // حساب المسافة
    double CalculateDistance(double lat1, double lon1, double lat2, double lon2);
}
```

---

### IAddressService

خدمة إدارة العناوين.

```csharp
public interface IAddressService
{
    Task<List<AddressResponseDto>> GetAddressesAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<AddressDetailDto?> GetAddressByIdAsync(Guid id, CancellationToken ct = default);
    Task<AddressResponseDto> CreateAddressAsync(CreateAddressDto dto, CancellationToken ct = default);
    Task<AddressResponseDto> UpdateAddressAsync(Guid id, UpdateAddressDto dto, CancellationToken ct = default);
    Task DeleteAddressAsync(Guid id, CancellationToken ct = default);
    Task SetDefaultAddressAsync(string entityType, Guid entityId, Guid addressId, CancellationToken ct = default);
}
```

---

## DTOs

### GeoSearchDto

```csharp
public class GeoSearchRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusKm { get; set; } = 10;      // نطاق البحث بالكيلومتر
    public int Limit { get; set; } = 20;
    public Guid? CountryId { get; set; }            // تصفية بالدولة
}

public class GeoSearchResult<T>
{
    public T Item { get; set; } = default!;
    public double DistanceKm { get; set; }          // المسافة من نقطة البحث
}

public class GeoPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public double DistanceTo(GeoPoint other)
    {
        // حساب المسافة باستخدام صيغة Haversine
    }
}
```

---

## مثال الاستخدام | Usage Example

```csharp
// الحصول على الدول النشطة
var countries = await _locationService.GetCountriesAsync(activeOnly: true);

// الحصول على مناطق السعودية
var regions = await _locationService.GetRegionsByCountryAsync(saudiArabiaId);

// البحث عن المدن القريبة
var nearbyCities = await _geoService.FindNearbyCitiesAsync(new GeoSearchRequest
{
    Latitude = 24.7136,
    Longitude = 46.6753,
    RadiusKm = 50
});

// البحث في المواقع
var results = await _locationService.SearchLocationsAsync("الرياض");
```

---

## أنماط التصميم | Design Patterns

| النمط | الاستخدام |
|-------|----------|
| **Hierarchical Pattern** | التسلسل: Country → Region → City → Neighborhood |
| **Polymorphic Association** | Address.EntityType للربط بأي كيان |
| **Soft Delete** | جميع الكيانات تدعم الحذف المنطقي |
| **Query Filter** | تصفية IsActive و IsDeleted تلقائياً |
