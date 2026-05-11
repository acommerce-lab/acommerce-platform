using ACommerce.Kits.Listings.Backend;
using ACommerce.Kits.Listings.Domain;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ashare.V3.Api.Enrichers;

/// <summary>
/// يُحَوِّل <see cref="ProductListingEntity"/> إلى shape ListingDetailDto
/// الذي تَتَوَقَّعه قالَب <c>ListingDetails.razor</c>:
/// <list type="bullet">
///   <item><b>Images</b>: مَفكوكَة مِن <c>ImagesJson</c> (JSON array of URLs).</item>
///   <item><b>Attributes</b>: مَفكوكَة مِن <c>AttributesJson</c> (key→value) ⇒
///         قائِمَة <c>{Key,Label,Value}</c> لِعَرضها كَ specifications dynamic.</item>
///   <item><b>Owner</b>: join مَع <see cref="ProfileEntity"/> لاسم البائِع.</item>
/// </list>
///
/// <para><b>لِماذا في التَطبيق لا في الكيت؟</b> Listings kit يَعمَل عَلى
/// واجِهَة <c>IListing</c> فَقَط — أَيّ App يُقَرِّر شَكل التَفاصيل الإضافي
/// (V3 Ashare لَه AttributesJson + ImagesJson؛ Ejar لَه ImagesCsv + أَعمِدَة
/// مُسَطَّحَة). الكيت يَستَدعي <c>IListingDetailEnricher</c> لَو سُجِّل،
/// ويُمَرِّر النَتيجَة كَ payload إلى الـ frontend.</para>
/// </summary>
public sealed class AshareV3ListingDetailEnricher : IListingDetailEnricher
{
    private readonly AshareV3DbContext _db;
    public AshareV3ListingDetailEnricher(AshareV3DbContext db) => _db = db;

    public async Task<object> EnrichAsync(IListing listing, CancellationToken ct)
    {
        // نَحتاج الـ entity الأَصلي لِنَصِل إلى ImagesJson + AttributesJson +
        // الحُقول الَّتي لَيسَت في IListing. نُعيد تَحميله مَرَّة (بِـ
        // AsNoTracking) — أَرخَص مِن كاش، أَدَقّ في الـ snapshot.
        if (!Guid.TryParse(listing.Id, out var listingId))
            return BuildFlat(listing, images: null, attributes: null, ownerName: null, memberSince: null);

        var entity = await _db.ProductListings
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == listingId, ct);

        if (entity is null)
            return BuildFlat(listing, images: null, attributes: null, ownerName: null, memberSince: null);

        var images     = ParseImages(entity.ImagesJson);
        var attributes = ParseAttributes(entity.AttributesJson);

        // Owner profile — اختياري، لَو فَشَل لا نُفجِّر الـ envelope.
        string? ownerName = null;
        string? memberSince = null;
        try
        {
            var owner = await _db.Profiles
                .AsNoTracking()
                .Where(p => p.Id == entity.VendorId)
                .Select(p => new { p.FullName, p.BusinessName, p.CreatedAt })
                .FirstOrDefaultAsync(ct);
            if (owner is not null)
            {
                ownerName = owner.BusinessName ?? owner.FullName;
                memberSince = owner.CreatedAt.ToString("yyyy");
            }
        }
        catch { /* أَطرَاف Profile الناقِصَة لا تَكسِر الـ details. */ }

        return BuildFlat(listing, images, attributes, ownerName, memberSince);
    }

    private static object BuildFlat(
        IListing l,
        List<string>? images,
        List<(string Key, string Label, string Value)>? attributes,
        string? ownerName,
        string? memberSince) => new
    {
        id           = l.Id,
        title        = l.Title,
        description  = l.Description,
        price        = l.Price,
        timeUnit     = l.TimeUnit,
        timeUnitLabel= l.TimeUnit switch
        {
            "monthly" => "شهرياً",
            "yearly"  => "سنوياً",
            "daily"   => "يومياً",
            "fixed"   => "",
            _         => l.TimeUnit
        },
        propertyType = l.PropertyType,
        propertyTypeLabel = l.PropertyType,   // لا join مَع DiscoveryCategories هُنا — kit details only.
        city         = l.City,
        district     = l.District,
        lat          = l.Lat,
        lng          = l.Lng,
        amenities    = Array.Empty<object>(), // V3 يَحفَظها داخِل AttributesJson — تُعرَض في attributes.
        ownerId      = l.OwnerId,
        owner = ownerName is null ? null : new { name = ownerName, memberSince },
        bedroomCount = l.BedroomCount,
        bathroomCount= l.BathroomCount,
        areaSqm      = l.AreaSqm,
        isVerified   = l.IsVerified,
        viewsCount   = l.ViewsCount,
        status       = l.Status,
        isFavorite   = false,
        images       = images ?? new List<string>(),
        attributes   = attributes?
            .Select(a => new { key = a.Key, label = a.Label, value = a.Value })
            .ToArray()
            ?? Array.Empty<object>(),
    };

    private static List<string>? ParseImages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(json);
            return arr?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }
        catch { return null; }
    }

    /// <summary>
    /// يُفَكِّك AttributesJson إلى قائِمَة <c>(key,label,value)</c>. يَدعَم
    /// أَشكال V2:
    /// <list type="bullet">
    ///   <item>كائِن مُسَطَّح: <c>{ "bedrooms": 3, "furnished": true }</c> ⇒
    ///         كُلّ مَفتاح يُصبِح خاصِّيَّة بِنَفس اسم الـ key كَ label.</item>
    ///   <item>كائِن مَعَ wrappers: <c>{ "bedrooms": { "value": 3, "label": "غُرَف" } }</c>
    ///         ⇒ الـ label يُستَخدَم لِلعَرض.</item>
    /// </list>
    /// </summary>
    private static List<(string Key, string Label, string Value)>? ParseAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            var result = new List<(string, string, string)>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var (label, value) = ExtractLabelValue(prop.Name, prop.Value);
                if (!string.IsNullOrWhiteSpace(value))
                    result.Add((prop.Name, label, value));
            }
            return result.Count > 0 ? result : null;
        }
        catch { return null; }
    }

    private static (string Label, string Value) ExtractLabelValue(string key, JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:  return (Humanize(key), el.GetString() ?? "");
            case JsonValueKind.Number:  return (Humanize(key), el.ToString());
            case JsonValueKind.True:    return (Humanize(key), "نعم");
            case JsonValueKind.False:   return (Humanize(key), "لا");
            case JsonValueKind.Object:
                // wrapper شَكل { value, label } أَو { value, name }
                var label = el.TryGetProperty("label", out var lEl) ? lEl.GetString() ?? Humanize(key) : Humanize(key);
                var value = el.TryGetProperty("value", out var vEl) ? vEl.ToString() : el.GetRawText();
                return (label, value);
            case JsonValueKind.Array:
                var items = new List<string>();
                foreach (var i in el.EnumerateArray())
                    items.Add(i.ValueKind == JsonValueKind.String ? i.GetString() ?? "" : i.ToString());
                return (Humanize(key), string.Join("، ", items));
            default: return (Humanize(key), "");
        }
    }

    /// <summary>تَحويل <c>property_name</c> أَو <c>propertyName</c> إلى <c>Property name</c>.</summary>
    private static string Humanize(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        var withSpaces = System.Text.RegularExpressions.Regex.Replace(
            key.Replace('_', ' '), "([a-z])([A-Z])", "$1 $2");
        return char.ToUpper(withSpaces[0]) + withSpaces[1..];
    }
}
