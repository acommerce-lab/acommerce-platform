using System.Text.Json;
using ACommerce.SharedKernel.Abstractions.DynamicAttributes;
using AshareMigrator.Legacy;
using AshareMigrator.Target;

namespace AshareMigrator.Mappers;

public static class ListingMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// يحوّل LegacyListing إلى NewListing، مبنياً لقطة الصفات من AttributesJson القديم
    /// عبر قالب الفئة الجديدة. أي مفتاح قديم لا يوجد في القالب يُحفظ حرفياً في الـ snapshot
    /// (type=text) حتى لا يضيع — القاعدة: لا نحذف بيانات.
    /// </summary>
    public static NewListing Map(
        LegacyListing src,
        Guid newOwnerId,
        Guid categoryId,
        AttributeTemplate? template)
    {
        var attrs = ParseAttrs(src.AttributesJson);

        // حقول مرقّاة إلى entity fields
        var licenseNumber = TakeString(attrs, "license_number");
        var isPhoneAllowed = TakeBool(attrs, "is_phone_allowed") ?? true;
        var isWhatsappAllowed = TakeBool(attrs, "is_whatsapp_allowed") ?? true;
        var isMessagingAllowed = TakeBool(attrs, "is_messaging_allowed") ?? true;

        // حقول مرقّاة إلى entity columns (تُستهلك من AttributesJson فقط إن وُجدت هناك)
        var duration = TakeInt(attrs, "duration") ?? 1;
        var timeUnit = TakeString(attrs, "time_unit") ?? "month";

        // الصور: ImagesJson هو JSON array من URLs → نحوّله إلى CSV
        var imagesCsv = ParseImagesJsonToCsv(src.ImagesJson);
        if (imagesCsv == null && !string.IsNullOrWhiteSpace(src.FeaturedImage))
            imagesCsv = src.FeaturedImage;

        // بناء lookup من القالب للتحقّق من وجود المفتاح
        var templateKeys = template?.Fields.Select(f => f.Key).ToHashSet() ?? new();

        // نفصل القيم إلى: داخل القالب (تُمرَّر إلى BuildSnapshot) + خارج القالب (تُضاف كـ raw text)
        var templateValues = new Dictionary<string, object?>();
        var extraAttrs = new List<DynamicAttribute>();
        var sortOrderCounter = 1000; // بعد كل حقول القالب الرسمية

        foreach (var (key, value) in attrs)
        {
            if (templateKeys.Contains(key))
                templateValues[key] = value;
            else if (value != null)
                extraAttrs.Add(new DynamicAttribute
                {
                    Key = key,
                    Label = key,
                    LabelAr = key,
                    Type = InferType(value),
                    Value = value,
                    DisplayValue = value.ToString(),
                    DisplayValueAr = value.ToString(),
                    SortOrder = sortOrderCounter++,
                    ShowInCard = false,
                });
        }

        List<DynamicAttribute> snapshot = template != null
            ? DynamicAttributeHelper.BuildSnapshot(template, templateValues).ToList()
            : new();
        snapshot.AddRange(extraAttrs);

        return new NewListing
        {
            Id = src.Id,
            CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = src.UpdatedAt,
            IsDeleted = false,
            OwnerId = newOwnerId,
            CategoryId = categoryId,
            Title = src.Title,
            Description = src.Description ?? "",
            Price = src.Price,
            Duration = duration,
            TimeUnit = timeUnit,
            Currency = src.Currency ?? "SAR",
            City = src.City ?? "",
            Latitude = src.Latitude,
            Longitude = src.Longitude,
            Address = src.Address,
            IsPhoneAllowed = isPhoneAllowed,
            IsWhatsAppAllowed = isWhatsappAllowed,
            IsMessagingAllowed = isMessagingAllowed,
            LicenseNumber = licenseNumber,
            ImagesCsv = imagesCsv,
            DynamicAttributesJson = DynamicAttributeHelper.SerializeAttributes(snapshot),
            Status = MapStatus(src.Status, src.IsActive),
            PublishedAt = src.IsActive ? src.CreatedAt : null,
            ViewCount = src.ViewCount,
            IsFeatured = src.IsFeatured,
        };
    }

    // ─── Helpers ───

    private static Dictionary<string, object?> ParseAttrs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                dict[prop.Name] = ConvertJson(prop.Value);
            return dict;
        }
        catch { return new(); }
    }

    private static object? ConvertJson(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => el.EnumerateArray().Select(ConvertJson).ToList(),
        JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJson(p.Value)),
        _ => null
    };

    private static string? TakeString(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return null;
        d.Remove(key);
        return v.ToString();
    }

    private static bool? TakeBool(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return null;
        d.Remove(key);
        if (v is bool b) return b;
        return bool.TryParse(v.ToString(), out var parsed) ? parsed : null;
    }

    private static int? TakeInt(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return null;
        d.Remove(key);
        if (v is long l) return (int)l;
        if (v is int i) return i;
        if (v is double dd) return (int)dd;
        return int.TryParse(v.ToString(), out var parsed) ? parsed : null;
    }

    private static string? ParseImagesJsonToCsv(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var urls = JsonSerializer.Deserialize<List<string>>(json, JsonOpts);
            return urls == null || urls.Count == 0 ? null : string.Join(",", urls);
        }
        catch { return null; }
    }

    private static string InferType(object v) => v switch
    {
        bool => "bool",
        int or long => "number",
        double or float or decimal => "decimal",
        List<object?> => "multi",
        _ => "text"
    };

    /// <summary>
    /// ListingStatus القديم: Draft=0, Active=1, Inactive=2, OutOfStock=3, Archived=4
    /// ListingStatus الجديد: Draft=0, Published=1, Reserved=2, Closed=3, Rejected=4
    /// </summary>
    private static int MapStatus(int legacy, bool isActive) => legacy switch
    {
        1 => 1,  // Active → Published
        2 => 3,  // Inactive → Closed
        3 => 2,  // OutOfStock → Reserved
        4 => 3,  // Archived → Closed
        _ => isActive ? 1 : 0
    };
}
