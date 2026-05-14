using System.Text.Json;

namespace ACommerce.Kits.DynamicAttributes.Operations;

/// <summary>
/// تَحويلات قِيَم الكِيان (Dictionary &lt;-&gt; JSON) — مَنطِق نَقي بِلا
/// EF/HTTP. الطَبَقَة العُليا في الكيت تَستَخدِمه لِبِناء snapshot عِندَ
/// القِراءَة + لِكِتابَة <c>AttributesJson</c> عِندَ التَعديل.
/// </summary>
public static class AttributeValues
{
    /// <summary>JSON object ⇒ قاموس بِحَساسِيَّة حالَة OrdinalIgnoreCase.</summary>
    public static Dictionary<string, object?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
                result[prop.Name] = ParseValue(prop.Value);
            return result;
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>قاموس قِيَم ⇒ JSON object (يَتَجاوَز null + الـ string فارِغ).</summary>
    public static string? Serialize(IReadOnlyDictionary<string, object?> values)
    {
        if (values is null || values.Count == 0) return null;
        var clean = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kv in values)
        {
            if (kv.Value is null) continue;
            if (kv.Value is string s && string.IsNullOrWhiteSpace(s)) continue;
            clean[kv.Key] = kv.Value;
        }
        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean);
    }

    private static object? ParseValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var i) ? i : (object)el.GetDouble(),
        JsonValueKind.True   => true,
        JsonValueKind.False  => false,
        JsonValueKind.Null   => null,
        JsonValueKind.Array  => el.EnumerateArray().Select(ParseValue).ToArray(),
        JsonValueKind.Object => el.GetRawText(),       // nested object → keep raw
        _                    => el.GetRawText(),
    };
}
