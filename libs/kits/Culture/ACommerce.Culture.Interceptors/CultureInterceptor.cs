using System.Collections;
using System.Reflection;
using ACommerce.Culture.Abstractions;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.Culture.Interceptors;

/// <summary>
/// يَلتَقِط المَغلَّفات المُوَسَّمة بـ <c>localize_times / localize_money /
/// translate_content</c> ويُحَوِّل DateTime UTC إلى timezone المُستَخدِم
/// المُسجَّل في <see cref="ICultureContext"/>. يُستَخدَم client-side عَن
/// طَريق <see cref="ApiReader"/> أو ما يُكافِئه.
/// </summary>
public sealed class CultureInterceptor
{
    private readonly ICultureContext _ctx;
    public CultureInterceptor(ICultureContext ctx) => _ctx = ctx;

    public bool AppliesTo<T>(OperationEnvelope<T> env, bool forced)
    {
        if (forced) return true;
        var tags = env?.Operation?.Tags;
        if (tags is null) return false;
        return Has(tags, "localize_times") || Has(tags, "localize_money") || Has(tags, "translate_content");
    }

    public Task LocalizeAsync<T>(OperationEnvelope<T> envelope, bool forced = false)
    {
        if (envelope is null || envelope.Data is null) return Task.CompletedTask;
        if (!AppliesTo(envelope, forced)) return Task.CompletedTask;
        Walk(envelope.Data, _ctx.TimeZone);
        return Task.CompletedTask;
    }

    private static bool Has(IDictionary<string, string> tags, string key)
        => tags.TryGetValue(key, out var v) && v == "true";

    private void Walk(object? node, TimeZoneInfo? tz, HashSet<object>? seen = null)
    {
        if (node is null) return;
        var t = node.GetType();
        if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)) return;

        seen ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!seen.Add(node)) return;

        if (node is IEnumerable enumerable)
        {
            foreach (var item in enumerable) Walk(item, tz, seen);
            return;
        }

        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
            var pt = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;

            if (pt == typeof(DateTime) && p.CanWrite)
            {
                if (p.GetValue(node) is DateTime dt && dt.Kind != DateTimeKind.Local)
                    p.SetValue(node, ConvertDate(dt, tz));
                continue;
            }

            if (pt.IsPrimitive || pt.IsEnum || pt == typeof(string) || pt == typeof(decimal)) continue;

            var child = p.GetValue(node);
            if (child is not null) Walk(child, tz, seen);
        }
    }

    private static DateTime ConvertDate(DateTime dt, TimeZoneInfo? tz)
    {
        var utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        if (tz is null) return DateTime.SpecifyKind(utc, DateTimeKind.Local);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, tz), DateTimeKind.Local);
    }
}
