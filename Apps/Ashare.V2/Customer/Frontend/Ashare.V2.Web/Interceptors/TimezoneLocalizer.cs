using System.Collections;
using System.Reflection;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Web.Store;

namespace Ashare.V2.Web.Interceptors;

/// <summary>
/// Post-display interceptor: يفحص OperationEnvelope القادم من الخادم، فإن
/// حمل الوسم <c>localize_times=true</c> أو طُلب صراحةً، يمشي على شجرة
/// <c>Data</c> (مصفوفات + records متداخلة) ويحوّل كلّ <c>DateTime</c>
/// من UTC إلى توقيت المتصفّح عبر <see cref="ITimezoneProvider"/>.
///
/// لماذا معترض لا تحويل في الصفحات؟
///   - الصفحات لا تعرف التوقيت؛ مسؤوليّة العرض فقط.
///   - التحويل يتمّ مرّة واحدة في حدود البيانات (<see cref="ApiReader"/>)
///     بدل تكراره في كلّ <c>@Tz.FormatTime(...)</c>.
///   - يصبح إضافة حقل جديد من نوع DateTime «مجّاني» — يُلتقط تلقائيّاً.
/// </summary>
public sealed class TimezoneLocalizer
{
    private readonly ITimezoneProvider _tz;
    public TimezoneLocalizer(ITimezoneProvider tz) => _tz = tz;

    public string Name => "TimezoneLocalizer";

    /// <summary>يُطبَّق إن حمل الـ envelope الوسم أو طلب المُنادي صراحةً.</summary>
    public bool AppliesTo<T>(OperationEnvelope<T> envelope, bool forced)
    {
        if (forced) return true;
        var tags = envelope?.Operation?.Tags;
        return tags is not null
            && tags.TryGetValue("localize_times", out var v)
            && v == "true";
    }

    public async Task LocalizeAsync<T>(OperationEnvelope<T> envelope, bool forced = false)
    {
        if (envelope is null || envelope.Data is null) return;
        if (!AppliesTo(envelope, forced)) return;
        await _tz.InitAsync();
        Walk(envelope.Data);
    }

    private void Walk(object? node, HashSet<object>? seen = null)
    {
        if (node is null) return;
        var t = node.GetType();
        if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)) return;

        seen ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!seen.Add(node)) return;

        if (node is IEnumerable enumerable)
        {
            foreach (var item in enumerable) Walk(item, seen);
            return;
        }

        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
            var pt = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;

            if (pt == typeof(DateTime))
            {
                if (!p.CanWrite) continue;
                var raw = p.GetValue(node);
                if (raw is DateTime dt && dt.Kind != DateTimeKind.Local)
                {
                    var utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    var local = DateTime.SpecifyKind(_tz.ToLocal(utc), DateTimeKind.Local);
                    p.SetValue(node, local);
                }
                continue;
            }

            if (pt.IsPrimitive || pt.IsEnum || pt == typeof(string) || pt == typeof(decimal)) continue;

            var child = p.GetValue(node);
            if (child is not null) Walk(child, seen);
        }
    }
}
