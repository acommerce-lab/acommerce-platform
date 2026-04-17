using ACommerce.SharedKernel.Abstractions.DynamicAttributes;
using Ashare.Api.Entities;

namespace Ashare.Api.Services;

/// <summary>
/// بذور الإعلانات: 16 إعلان حقيقي مستخرج من بيانات الإنتاج (مشاركة شقق في الرياض)
/// + 8 منتجات اختبارية تغطي الفئات الأخرى.
///
/// كل عرض يحمل لقطة <see cref="DynamicAttribute"/> مبنية من قالب فئته،
/// لذلك تعديل القالب لاحقاً لا يؤثر على هذه الإعلانات.
/// </summary>
internal static class AshareListingsSeed
{
    public static IEnumerable<Listing> All(DateTime now, Guid ownerId)
    {
        // ---- 16 إعلان مشاركة سكنية حقيقية في الرياض ----
        yield return Residential(now, ownerId,
            "للمشاركة غرفه وصاله في حي العارض / بلاطة",
            "غرفة وصالة جاهزة للمشاركة، حي العارض، شمال الرياض. السعر سنوي.",
            price: 37000, time: "year", district: "العارض", lat: 24.872, lng: 46.638,
            propertyType: "building", rooms: 1, bathrooms: 1, area: 50, floor: 2,
            furnished: "semi", amenities: new() { "ac", "kitchen" });

        yield return Residential(now, ownerId,
            "للمشاركة غرفتين وصاله في حي العارض",
            "غرفتين وصالة، حي العارض، مناسبة لشخصين أو عائلة صغيرة.",
            price: 41000, time: "year", district: "العارض", lat: 24.876, lng: 46.640,
            propertyType: "building", rooms: 2, bathrooms: 2, area: 80, floor: 1,
            furnished: "semi", amenities: new() { "ac", "kitchen", "parking" });

        yield return Residential(now, ownerId,
            "للمشاركة باليومي 3 غرف في حي الملقا / مفروشة",
            "ثلاث غرف مفروشة بالكامل، حي الملقا. السعر باليوم.",
            price: 550, time: "day", district: "الملقا", lat: 24.795, lng: 46.628,
            propertyType: "building", rooms: 3, bathrooms: 2, area: 120, floor: 3,
            furnished: "furnished", amenities: new() { "wifi", "ac", "kitchen", "parking" });

        yield return Residential(now, ownerId,
            "غرفة فاخرة للمشاركة في حي غرناطة",
            "غرفة كبيرة بحمام خاص، حي غرناطة، شرق الرياض.",
            price: 1800, time: "month", district: "غرناطة", lat: 24.793, lng: 46.766,
            propertyType: "building", rooms: 1, bathrooms: 1, area: 25,
            furnished: "furnished", amenities: new() { "wifi", "ac" });

        yield return Residential(now, ownerId,
            "شقة للمشاركة في حي حطين",
            "شقة 4 غرف للمشاركة بين الطلاب أو الموظفين.",
            price: 2200, time: "month", district: "حطين", lat: 24.776, lng: 46.602,
            propertyType: "building", rooms: 4, bathrooms: 3, area: 160, floor: 5,
            furnished: "semi", amenities: new() { "ac", "elevator", "parking" });

        yield return Residential(now, ownerId,
            "للمشاركة في حي الصحافة / غرفة وحمام",
            "غرفة بحمام خاص، حي الصحافة، شمال الرياض.",
            price: 1500, time: "month", district: "الصحافة", lat: 24.794, lng: 46.667,
            propertyType: "building", rooms: 1, bathrooms: 1, area: 22,
            furnished: "furnished", amenities: new() { "wifi", "ac" });

        yield return Residential(now, ownerId,
            "غرفة للمشاركة بسعر مميز في حي العارض",
            "غرفة مفروشة بسعر اقتصادي للطلاب.",
            price: 350, time: "month", district: "العارض", lat: 24.874, lng: 46.635,
            propertyType: "building", rooms: 1, bathrooms: 1, area: 18,
            furnished: "furnished", amenities: new() { "ac" });

        yield return Residential(now, ownerId,
            "للمشاركة شقة في حي الملقا — قريبة من جامعة الملك سعود",
            "شقة 3 غرف للمشاركة، قريبة من الجامعة والمولات.",
            price: 38000, time: "year", district: "الملقا", lat: 24.798, lng: 46.625,
            propertyType: "building", rooms: 3, bathrooms: 2, area: 130, floor: 4,
            furnished: "semi", amenities: new() { "ac", "elevator", "parking", "security" });

        yield return Residential(now, ownerId,
            "غرفة فاخرة للموظفات — حي غرناطة",
            "للموظفات فقط. غرفة بحمام خاص في شقة عائلية.",
            price: 2000, time: "month", district: "غرناطة", lat: 24.790, lng: 46.770,
            propertyType: "building", rooms: 1, bathrooms: 1, area: 30,
            furnished: "furnished", genderPref: "female",
            amenities: new() { "wifi", "ac", "kitchen", "laundry" });

        yield return Residential(now, ownerId,
            "للمشاركة بالأسبوع — حي حطين",
            "غرفة وصالة بالأسبوع لزائري الرياض.",
            price: 850, time: "week", district: "حطين", lat: 24.780, lng: 46.600,
            propertyType: "building", rooms: 1, bathrooms: 1, area: 40,
            furnished: "furnished", amenities: new() { "wifi", "ac", "kitchen" });

        yield return Residential(now, ownerId,
            "غرفتين للمشاركة في حي الصحافة",
            "غرفتين مع صالة، مناسبة لمشاركة عائلية صغيرة.",
            price: 2500, time: "month", district: "الصحافة", lat: 24.795, lng: 46.665,
            propertyType: "building", rooms: 2, bathrooms: 2, area: 75, floor: 2,
            furnished: "semi", amenities: new() { "ac", "kitchen", "parking" });

        yield return Residential(now, ownerId,
            "غرفة في فيلا — حي الملقا",
            "غرفة داخل فيلا عائلية، حي الملقا.",
            price: 1400, time: "month", district: "الملقا", lat: 24.797, lng: 46.629,
            propertyType: "villa", rooms: 1, bathrooms: 1, area: 20,
            furnished: "furnished", genderPref: "male",
            amenities: new() { "wifi", "ac", "parking", "garden" });

        yield return Residential(now, ownerId,
            "شقة كاملة للمشاركة — حي العارض",
            "شقة كاملة 3 غرف للمشاركة بين 3 أشخاص.",
            price: 45000, time: "year", district: "العارض", lat: 24.870, lng: 46.642,
            propertyType: "building", rooms: 3, bathrooms: 2, area: 110, floor: 3,
            furnished: "semi", amenities: new() { "ac", "elevator", "parking" });

        yield return Residential(now, ownerId,
            "غرفة فاخرة للمشاركة في حي حطين",
            "غرفة فسيحة في شقة فاخرة بحي حطين.",
            price: 2800, time: "month", district: "حطين", lat: 24.778, lng: 46.605,
            propertyType: "building", rooms: 1, bathrooms: 1, area: 28,
            furnished: "furnished", amenities: new() { "wifi", "ac", "gym", "pool" });

        yield return Residential(now, ownerId,
            "للمشاركة باليومي — حي غرناطة / شقة جاهزة",
            "شقة جاهزة باليومي في حي غرناطة.",
            price: 480, time: "day", district: "غرناطة", lat: 24.792, lng: 46.768,
            propertyType: "building", rooms: 2, bathrooms: 1, area: 65,
            furnished: "furnished", amenities: new() { "wifi", "ac", "kitchen", "parking" });

        yield return Residential(now, ownerId,
            "غرفة للمشاركة في حي الصحافة — قريبة من المترو",
            "غرفة مفروشة قريبة من محطة المترو.",
            price: 1700, time: "month", district: "الصحافة", lat: 24.796, lng: 46.668,
            propertyType: "building", rooms: 1, bathrooms: 1, area: 24,
            furnished: "furnished", amenities: new() { "wifi", "ac" });

        // ---- 8 منتجات اختبارية لتغطية بقية الفئات ----
        yield return Residential(now, ownerId,
            "شقة مفروشة في حي النرجس",
            "شقة 3 غرف نوم مفروشة بالكامل، تشطيب فاخر.",
            price: 3500, time: "month", district: "النرجس", lat: 24.880, lng: 46.690,
            propertyType: "apartment", rooms: 3, bathrooms: 2, area: 140, floor: 4,
            furnished: "furnished", amenities: new() { "wifi", "ac", "elevator", "parking" });

        yield return Residential(now, ownerId,
            "استوديو فاخر في حي الملقا",
            "استوديو حديث، مفروش بالكامل، إطلالة على الحديقة.",
            price: 2500, time: "month", district: "الملقا", lat: 24.795, lng: 46.628,
            propertyType: "studio", rooms: 1, bathrooms: 1, area: 45, floor: 6,
            furnished: "furnished", amenities: new() { "wifi", "ac", "elevator", "gym", "pool" });

        yield return Residential(now, ownerId,
            "فيلا واسعة في حي الياسمين",
            "فيلا دورين، حديقة خاصة، مسبح، 5 غرف نوم.",
            price: 15000, time: "month", district: "الياسمين", lat: 24.835, lng: 46.660,
            propertyType: "villa", rooms: 5, bathrooms: 4, area: 450,
            furnished: "unfurnished", amenities: new() { "parking", "garden", "pool", "security" });

        yield return Commercial(now, ownerId,
            "محل تجاري في طريق الملك فهد",
            "محل بمساحة 80م² على طريق الملك فهد، واجهة زجاجية.",
            price: 10000, time: "month", district: "العليا", lat: 24.706, lng: 46.685,
            propertyType: "shop", area: 80, floor: 0, parking: "street",
            workingHours: "24_7", amenities: new() { "ac", "security" });

        yield return Commercial(now, ownerId,
            "مطعم جاهز للتشغيل في حي الورود",
            "مطعم مجهز بالكامل، مطبخ + صالة 60 شخص.",
            price: 18000, time: "month", district: "الورود", lat: 24.710, lng: 46.682,
            propertyType: "restaurant", area: 200, capacity: 60, parking: "private",
            workingHours: "business", amenities: new() { "ac", "kitchen", "parking" });

        yield return Administrative(now, ownerId,
            "مكتب مجهز في برج المملكة",
            "مكتب 50م² في برج المملكة، مفروش بالكامل، إطلالة بانورامية.",
            price: 8000, time: "month", district: "العليا", lat: 24.711, lng: 46.674,
            propertyType: "office", area: 50, floor: 35, capacity: 6, parking: "garage",
            workingHours: "business", amenities: new() { "wifi", "ac", "elevator", "security" });

        yield return Administrative(now, ownerId,
            "قاعة اجتماعات VIP",
            "قاعة اجتماعات بسعة 12 شخص، مع جهاز عرض ومرطبات.",
            price: 500, time: "day", district: "العليا", lat: 24.708, lng: 46.680,
            propertyType: "meeting_room", area: 35, capacity: 12, parking: "garage",
            workingHours: "business", amenities: new() { "wifi", "ac", "elevator" });

        yield return Administrative(now, ownerId,
            "طابق إداري كامل — حي الواحة",
            "طابق إداري كامل 800م²، مناسب لشركة 50 موظف.",
            price: 50000, time: "month", district: "الواحة", lat: 24.741, lng: 46.715,
            propertyType: "full_floor", area: 800, floor: 8, capacity: 50, parking: "garage",
            workingHours: "business", amenities: new() { "wifi", "ac", "elevator", "parking", "security" });
    }

    // ===== Builders =====

    private static Listing Residential(DateTime now, Guid ownerId,
        string title, string description, decimal price, string time, string district,
        double lat, double lng,
        string propertyType, int? rooms = null, int? bathrooms = null,
        double? area = null, int? floor = null, string? furnished = null,
        string? rentalType = "shared", string? billType = "offer",
        string? genderPref = null, List<string>? amenities = null)
    {
        var values = new Dictionary<string, object?>
        {
            ["property_type"] = propertyType,
            ["rental_type"] = rentalType,
            ["bill_type"] = billType,
            ["furnished"] = furnished,
            ["rooms"] = rooms,
            ["bathrooms"] = bathrooms,
            ["area"] = area,
            ["floor"] = floor,
            ["amenities"] = amenities ?? new(),
            ["gender_pref"] = genderPref,
        };

        var snapshot = DynamicAttributeHelper.BuildSnapshot(AshareCategoryTemplates.Residential(), values);

        return new Listing
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            OwnerId = ownerId,
            CategoryId = AshareSeeder.CategoryIds.Residential,
            Title = title,
            Description = description,
            Price = price,
            Duration = 1,
            TimeUnit = time,
            City = "الرياض",
            District = district,
            Latitude = lat,
            Longitude = lng,
            DynamicAttributesJson = DynamicAttributeHelper.SerializeAttributes(snapshot),
            Status = ListingStatus.Published,
            PublishedAt = now
        };
    }

    private static Listing Commercial(DateTime now, Guid ownerId,
        string title, string description, decimal price, string time, string district,
        double lat, double lng,
        string propertyType, double? area = null, int? floor = null, int? capacity = null,
        string? parking = null, string? workingHours = null, List<string>? amenities = null)
    {
        var values = new Dictionary<string, object?>
        {
            ["property_type"] = propertyType,
            ["area"] = area,
            ["floor"] = floor,
            ["capacity"] = capacity,
            ["parking"] = parking,
            ["working_hours"] = workingHours,
            ["amenities"] = amenities ?? new(),
        };

        var snapshot = DynamicAttributeHelper.BuildSnapshot(AshareCategoryTemplates.Commercial(), values);

        return new Listing
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            OwnerId = ownerId,
            CategoryId = AshareSeeder.CategoryIds.Commercial,
            Title = title,
            Description = description,
            Price = price,
            Duration = 1,
            TimeUnit = time,
            City = "الرياض",
            District = district,
            Latitude = lat,
            Longitude = lng,
            DynamicAttributesJson = DynamicAttributeHelper.SerializeAttributes(snapshot),
            Status = ListingStatus.Published,
            PublishedAt = now
        };
    }

    private static Listing Administrative(DateTime now, Guid ownerId,
        string title, string description, decimal price, string time, string district,
        double lat, double lng,
        string propertyType, double? area = null, int? floor = null, int? capacity = null,
        string? parking = null, string? workingHours = null, List<string>? amenities = null)
    {
        var values = new Dictionary<string, object?>
        {
            ["property_type"] = propertyType,
            ["area"] = area,
            ["floor"] = floor,
            ["capacity"] = capacity,
            ["parking"] = parking,
            ["working_hours"] = workingHours,
            ["amenities"] = amenities ?? new(),
        };

        var snapshot = DynamicAttributeHelper.BuildSnapshot(AshareCategoryTemplates.Administrative(), values);

        return new Listing
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            OwnerId = ownerId,
            CategoryId = AshareSeeder.CategoryIds.Administrative,
            Title = title,
            Description = description,
            Price = price,
            Duration = 1,
            TimeUnit = time,
            City = "الرياض",
            District = district,
            Latitude = lat,
            Longitude = lng,
            DynamicAttributesJson = DynamicAttributeHelper.SerializeAttributes(snapshot),
            Status = ListingStatus.Published,
            PublishedAt = now
        };
    }
}
