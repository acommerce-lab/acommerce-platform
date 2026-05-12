using ACommerce.Kits.Listings.Backend;
using ACommerce.Kits.Listings.Domain;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ashare.V3.Api.Enrichers;

/// <summary>
/// يُحَوِّل <see cref="ProductListingEntity"/> إلى shape ListingDetailDto.
///
/// <para><b>الخَصائِص الديناميكِيَّة (السِمات)</b>: يَتَّبِع نَمَط
/// Template+Snapshot القائِم في المِنَصَّة:</para>
/// <list type="bullet">
///   <item>يُحَدِّد القالَب مِن DB (<c>CategoryAttributeTemplates</c>) بِناءً
///         عَلى <see cref="ProductCategoryEntity.Slug"/> لِفِئَة الإعلان.
///         fallback إلى الكود لَو DB row مَفقود.</item>
///   <item>يَفُكّ <c>AttributesJson</c> الإنتاجي القَديم (كائِن مُسَطَّح
///         <c>{ bedrooms: 3, furnished: "yes" }</c>) إلى قاموس قِيَم.</item>
///   <item>يَستَدعي <see cref="DynamicAttributeHelper.BuildSnapshot"/> ⇒
///         يَخلِط مَع القالَب لِيُنتِج <see cref="DynamicAttribute"/>
///         مُكتَمِلَة (Label/LabelAr/Icon/DisplayValue مُجَمَّدَة).</item>
///   <item>يَدعَم بَيانات قَديمَة لِفِئة بِلا قالَب: يُنتِج
///         <c>DynamicAttribute</c> خام (key=Key، Label=Key) لِتَجَنُّب
///         فَقد بَيانات.</item>
/// </list>
/// </summary>
public sealed class AshareV3ListingDetailEnricher : IListingDetailEnricher
{
    private readonly AshareV3DbContext _db;
    private readonly ProductionAttributeTemplateSource _prodSource;
    public AshareV3ListingDetailEnricher(AshareV3DbContext db,
                                         ProductionAttributeTemplateSource prodSource)
    {
        _db = db;
        _prodSource = prodSource;
    }

    public async Task<object> EnrichAsync(IListing listing, CancellationToken ct)
    {
        if (!Guid.TryParse(listing.Id, out var listingId))
            return BuildFlat(listing, images: null, attributes: null, ownerName: null, memberSince: null, ownerAvatar: null);

        var entity = await _db.ProductListings
            .AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == listingId, ct);
        if (entity is null)
            return BuildFlat(listing, images: null, attributes: null, ownerName: null, memberSince: null, ownerAvatar: null);

        var images     = ParseImages(entity.ImagesJson);
        var attributes = await BuildAttributesAsync(entity, ct);

        // Owner profile — اختياري.
        string? ownerName = null;
        string? memberSince = null;
        string? ownerAvatar = null;
        try
        {
            var owner = await _db.Profiles.AsNoTracking()
                .Where(p => p.Id == entity.VendorId)
                .Select(p => new { p.FullName, p.BusinessName, p.CreatedAt, p.Avatar })
                .FirstOrDefaultAsync(ct);
            if (owner is not null)
            {
                ownerName   = owner.BusinessName ?? owner.FullName;
                memberSince = owner.CreatedAt.ToString("yyyy");
                ownerAvatar = owner.Avatar;
            }
        }
        catch { /* فَقد Profile لا يَكسِر التَفاصيل. */ }

        return BuildFlat(listing, images, attributes, ownerName, memberSince, ownerAvatar);
    }

    private async Task<List<DynamicAttribute>> BuildAttributesAsync(
        ProductListingEntity entity, CancellationToken ct)
    {
        var rawValues = ParseLegacyAttributes(entity.AttributesJson);
        if (rawValues.Count == 0) return new();

        AttributeTemplate? template = null;

        // ① مَصدَر الإنتاج (CategoryAttributeMappings + AttributeDefinitions
        //    + AttributeValues) — كانوني.
        if (entity.CategoryId is { } catId)
            template = await _prodSource.BuildForCategoryAsync(catId, ct);

        // ② Fallback إلى DB row (admin-edited).
        if (template is null || template.Fields.Count == 0)
        {
            string? categorySlug = null;
            if (entity.CategoryId is { } cid2)
            {
                categorySlug = await _db.ProductCategories.AsNoTracking()
                    .Where(c => c.Id == cid2).Select(c => c.Slug)
                    .FirstOrDefaultAsync(ct);
            }

            if (!string.IsNullOrEmpty(categorySlug))
            {
                var row = await _db.CategoryAttributeTemplates.AsNoTracking()
                    .Where(t => t.CategorySlug == categorySlug)
                    .Select(t => t.TemplateJson).FirstOrDefaultAsync(ct);
                if (!string.IsNullOrEmpty(row))
                    template = DynamicAttributeHelper.ParseTemplate(row);
            }
        }

        // ③ لا قالَب ⇒ snapshot خام بِالـ keys (لا labels مُختَرَعَة).
        if (template is null || template.Fields.Count == 0)
            return RawSnapshot(rawValues);

        // ④ Template + values ⇒ snapshot كامِل عَبر الـ helper.
        return DynamicAttributeHelper.BuildSnapshot(template, rawValues);
    }

    /// <summary>
    /// يَفُكّ JSON القَديم (شَكل V2: كائِن مُسَطَّح <c>{ key: value }</c>)
    /// إلى <c>Dictionary&lt;string, object?&gt;</c> بِأَنواع .NET أَصلِيَّة
    /// (لِيَتَوافَق مَع <c>BuildSnapshot</c>).
    /// </summary>
    private static Dictionary<string, object?> ParseLegacyAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new();
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
                result[prop.Name] = ExtractValue(prop.Value);
            return result;
        }
        catch { return new(); }
    }

    private static object? ExtractValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var i) ? i : (object)el.GetDouble(),
        JsonValueKind.True   => true,
        JsonValueKind.False  => false,
        JsonValueKind.Null   => null,
        JsonValueKind.Array  => el.EnumerateArray().Select(ExtractValue).ToList(),
        JsonValueKind.Object => el.TryGetProperty("value", out var v) ? ExtractValue(v) : el.GetRawText(),
        _ => null,
    };

    /// <summary>snapshot بِلا قالَب — مَفاتيح خام لِتَجَنُّب فِقد بَيانات.</summary>
    private static List<DynamicAttribute> RawSnapshot(Dictionary<string, object?> raw)
    {
        var i = 0;
        return raw.Where(kv => kv.Value is not null)
            .Select(kv => new DynamicAttribute
            {
                Key = kv.Key,
                Label = kv.Key,
                LabelAr = kv.Key,
                Type = kv.Value is bool ? "bool" : (kv.Value is long or double ? "number" : "text"),
                Value = kv.Value,
                SortOrder = ++i,
            }).ToList();
    }

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

    private static object BuildFlat(
        IListing l,
        List<string>? images,
        List<DynamicAttribute>? attributes,
        string? ownerName,
        string? memberSince,
        string? ownerAvatar) => new
    {
        id           = l.Id,
        title        = l.Title,
        description  = l.Description,
        price        = l.Price,
        timeUnit     = l.TimeUnit,
        timeUnitLabel= l.TimeUnit,            // الواجِهَة تُتَرجِم — لا labels مَحفورَة هُنا
        propertyType = l.PropertyType,
        propertyTypeLabel = l.PropertyType,
        city         = l.City,
        district     = l.District,
        lat          = l.Lat,
        lng          = l.Lng,
        amenities    = Array.Empty<object>(),
        ownerId      = l.OwnerId,
        owner        = ownerName is null ? null : new { name = ownerName, memberSince, avatarUrl = ownerAvatar },
        bedroomCount = l.BedroomCount,
        bathroomCount= l.BathroomCount,
        areaSqm      = l.AreaSqm,
        isVerified   = l.IsVerified,
        viewsCount   = l.ViewsCount,
        status       = l.Status,
        isFavorite   = false,
        images       = images ?? new List<string>(),
        attributes   = attributes ?? new List<DynamicAttribute>(),
    };
}
