# ACommerce.Locations.Api

## معلومات المشروع | Project Info

| الخاصية | القيمة |
|---------|--------|
| **المسار** | `Other/ACommerce.Locations.Api` |
| **النوع** | Class Library |
| **الإطار** | .NET 9.0 |
| **الاعتماديات** | `ACommerce.Locations.Abstractions`, `Microsoft.AspNetCore.Mvc.Core` |

## الوصف | Description

مكتبة API Controllers جاهزة للاستخدام لنظام المواقع الجغرافية. توفر نقاط نهاية RESTful للوصول إلى الدول، المناطق، المدن، والأحياء.

---

## الهيكل | Structure

```
ACommerce.Locations.Api/
└── Controllers/
    ├── CountriesController.cs
    ├── RegionsController.cs
    ├── CitiesController.cs
    ├── NeighborhoodsController.cs
    └── LocationSearchController.cs
```

---

## نقاط النهاية | API Endpoints

### Countries (الدول)

| Method | Endpoint | الوصف |
|--------|----------|-------|
| GET | `/api/locations/countries` | الحصول على جميع الدول |
| GET | `/api/locations/countries/{id}` | الحصول على دولة بالمعرف |
| GET | `/api/locations/countries/by-code/{code}` | الحصول على دولة بالرمز (SA) |
| GET | `/api/locations/countries/{id}/regions` | الحصول على مناطق دولة |
| GET | `/api/locations/countries/{id}/cities` | الحصول على كل مدن دولة |

### Regions (المناطق)

| Method | Endpoint | الوصف |
|--------|----------|-------|
| GET | `/api/locations/regions/{id}` | الحصول على منطقة بالمعرف |
| GET | `/api/locations/regions/{id}/cities` | الحصول على مدن منطقة |

### Cities (المدن)

| Method | Endpoint | الوصف |
|--------|----------|-------|
| GET | `/api/locations/cities/{id}` | الحصول على مدينة بالمعرف |
| GET | `/api/locations/cities/{id}/neighborhoods` | الحصول على أحياء مدينة |

### Neighborhoods (الأحياء)

| Method | Endpoint | الوصف |
|--------|----------|-------|
| GET | `/api/locations/neighborhoods/{id}` | الحصول على حي بالمعرف |

### Search (البحث)

| Method | Endpoint | الوصف |
|--------|----------|-------|
| GET | `/api/locations/search?q={query}` | البحث في المواقع |
| GET | `/api/locations/hierarchy` | الحصول على التسلسل الهرمي |
| GET | `/api/locations/nearby` | البحث عن المواقع القريبة |
| GET | `/api/locations/reverse-geocode` | تحديد الموقع العكسي |

---

## Controllers

### CountriesController

```csharp
[ApiController]
[Route("api/locations/countries")]
public class CountriesController : ControllerBase
{
    private readonly ILocationService _locationService;

    public CountriesController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    /// <summary>
    /// الحصول على جميع الدول
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CountryResponseDto>>> GetCountries(
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var countries = await _locationService.GetCountriesAsync(activeOnly, ct);
        return Ok(countries);
    }

    /// <summary>
    /// الحصول على دولة بالمعرف
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CountryDetailDto>> GetCountry(
        Guid id,
        CancellationToken ct = default)
    {
        var country = await _locationService.GetCountryByIdAsync(id, ct);
        if (country == null) return NotFound();
        return Ok(country);
    }

    /// <summary>
    /// الحصول على دولة بالرمز
    /// </summary>
    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<CountryResponseDto>> GetCountryByCode(
        string code,
        CancellationToken ct = default)
    {
        var country = await _locationService.GetCountryByCodeAsync(code, ct);
        if (country == null) return NotFound();
        return Ok(country);
    }

    /// <summary>
    /// الحصول على مناطق دولة
    /// </summary>
    [HttpGet("{id:guid}/regions")]
    public async Task<ActionResult<List<RegionResponseDto>>> GetCountryRegions(
        Guid id,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var regions = await _locationService.GetRegionsByCountryAsync(id, activeOnly, ct);
        return Ok(regions);
    }

    /// <summary>
    /// الحصول على مدن دولة (كل المدن)
    /// </summary>
    [HttpGet("{id:guid}/cities")]
    public async Task<ActionResult<List<CityResponseDto>>> GetCountryCities(
        Guid id,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var cities = await _locationService.GetCitiesByCountryAsync(id, activeOnly, ct);
        return Ok(cities);
    }
}
```

---

### LocationSearchController

```csharp
[ApiController]
[Route("api/locations")]
public class LocationSearchController : ControllerBase
{
    private readonly ILocationService _locationService;
    private readonly IGeoService _geoService;

    public LocationSearchController(
        ILocationService locationService,
        IGeoService geoService)
    {
        _locationService = locationService;
        _geoService = geoService;
    }

    /// <summary>
    /// البحث في المواقع
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<LocationSearchResult>>> Search(
        [FromQuery] string q,
        [FromQuery] Guid? countryId = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var results = await _locationService.SearchLocationsAsync(q, countryId, limit, ct);
        return Ok(results);
    }

    /// <summary>
    /// الحصول على التسلسل الهرمي لموقع
    /// </summary>
    [HttpGet("hierarchy")]
    public async Task<ActionResult<LocationHierarchyDto>> GetHierarchy(
        [FromQuery] Guid? neighborhoodId = null,
        [FromQuery] Guid? cityId = null,
        [FromQuery] Guid? regionId = null,
        [FromQuery] Guid? countryId = null,
        CancellationToken ct = default)
    {
        var hierarchy = await _locationService.GetLocationHierarchyAsync(
            neighborhoodId, cityId, regionId, countryId, ct);

        if (hierarchy == null) return NotFound();
        return Ok(hierarchy);
    }

    /// <summary>
    /// البحث عن المدن القريبة
    /// </summary>
    [HttpGet("nearby")]
    public async Task<ActionResult<List<GeoSearchResult<CityResponseDto>>>> GetNearbyCities(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] double radius = 50,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var cities = await _geoService.FindNearbyCitiesAsync(new GeoSearchRequest
        {
            Latitude = lat,
            Longitude = lon,
            RadiusKm = radius,
            Limit = limit
        }, ct);

        return Ok(cities);
    }

    /// <summary>
    /// تحديد الموقع العكسي من الإحداثيات
    /// </summary>
    [HttpGet("reverse-geocode")]
    public async Task<ActionResult<LocationHierarchyDto>> ReverseGeocode(
        [FromQuery] double lat,
        [FromQuery] double lon,
        CancellationToken ct = default)
    {
        var location = await _geoService.ReverseGeocodeAsync(lat, lon, ct);
        if (location == null) return NotFound();
        return Ok(location);
    }
}
```

---

## التكامل | Integration

### إضافة Controllers إلى مشروعك

```csharp
// في Program.cs
var builder = WebApplication.CreateBuilder(args);

// تسجيل الخدمات
builder.Services.AddACommerceLocations();

// إضافة Controllers من المكتبة
builder.Services.AddControllers()
    .AddApplicationPart(typeof(CountriesController).Assembly);

var app = builder.Build();

app.MapControllers();
app.Run();
```

---

## أمثلة الاستخدام | Usage Examples

### JavaScript/TypeScript (Fetch)

```javascript
// الحصول على الدول
const countries = await fetch('/api/locations/countries').then(r => r.json());

// الحصول على مناطق السعودية
const saudiId = 'guid-here';
const regions = await fetch(`/api/locations/countries/${saudiId}/regions`).then(r => r.json());

// البحث في المواقع
const results = await fetch('/api/locations/search?q=الرياض').then(r => r.json());

// البحث عن المدن القريبة
const nearby = await fetch('/api/locations/nearby?lat=24.7136&lon=46.6753&radius=50')
    .then(r => r.json());
```

### C# (HttpClient)

```csharp
// الحصول على الدول
var countries = await httpClient.GetFromJsonAsync<List<CountryResponseDto>>(
    "/api/locations/countries");

// الحصول على دولة بالرمز
var saudi = await httpClient.GetFromJsonAsync<CountryResponseDto>(
    "/api/locations/countries/by-code/SA");

// البحث
var results = await httpClient.GetFromJsonAsync<List<LocationSearchResult>>(
    "/api/locations/search?q=جدة");
```

---

## Response Examples

### GET /api/locations/countries

```json
[
  {
    "id": "guid",
    "name": "المملكة العربية السعودية",
    "nameEn": "Saudi Arabia",
    "code": "SA",
    "phoneCode": "+966",
    "flag": "🇸🇦",
    "isActive": true
  }
]
```

### GET /api/locations/search?q=الرياض

```json
[
  {
    "id": "guid",
    "name": "الرياض",
    "nameEn": "Riyadh",
    "level": 3,
    "parentName": "منطقة الرياض",
    "fullPath": "السعودية > منطقة الرياض > الرياض"
  },
  {
    "id": "guid",
    "name": "منطقة الرياض",
    "nameEn": "Riyadh Region",
    "level": 2,
    "parentName": "المملكة العربية السعودية",
    "fullPath": "السعودية > منطقة الرياض"
  }
]
```

### GET /api/locations/nearby?lat=24.7136&lon=46.6753&radius=100

```json
[
  {
    "item": {
      "id": "guid",
      "name": "الرياض",
      "nameEn": "Riyadh"
    },
    "distanceKm": 0.5
  },
  {
    "item": {
      "id": "guid",
      "name": "الخرج",
      "nameEn": "Al Kharj"
    },
    "distanceKm": 77.3
  }
]
```

### GET /api/locations/hierarchy?cityId={guid}

```json
{
  "country": {
    "id": "guid",
    "name": "المملكة العربية السعودية",
    "code": "SA"
  },
  "region": {
    "id": "guid",
    "name": "منطقة الرياض",
    "code": "RUH"
  },
  "city": {
    "id": "guid",
    "name": "الرياض",
    "isCapital": true
  },
  "neighborhood": null
}
```

---

## Query Parameters

### Common Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `activeOnly` | bool | true | إرجاع النشطة فقط |

### Search Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `q` | string | required | نص البحث |
| `countryId` | Guid? | null | تصفية بالدولة |
| `limit` | int | 20 | عدد النتائج |

### Geo Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `lat` | double | required | خط العرض |
| `lon` | double | required | خط الطول |
| `radius` | double | 50 | نطاق البحث (كم) |

---

## HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | نجاح العملية |
| 404 | الموقع غير موجود |
| 400 | طلب غير صالح |
| 500 | خطأ في الخادم |
