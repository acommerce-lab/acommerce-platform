using Microsoft.JSInterop;

namespace Ashare.V2.Web.Store;

/// <summary>
/// ProviderContract: abstracts the "browser's timezone" — an external dependency
/// the app MUST have to display UTC times correctly.
///
/// لماذا ProviderContract لا Service؟
///   - المنهجيّة (CLAUDE.md) تُصنّف أيّ اعتماد خارجيّ إلزاميّ كـ ProviderContract
///     (نفس تصنيف IPaymentGateway, IMessageStore).
///   - خدمة التوقيت تعتمد على المتصفّح (أو رأس HTTP، أو IANA lookup) —
///     هذه مصادر خارجيّة، لا منطق أعمال.
///   - العقد يسمح بتبديل الـ implementation بدون تعديل المستخدمين
///     (JS في Blazor Server، headers في Wasm، stub في الاختبارات).
/// </summary>
public interface ITimezoneProvider
{
    /// <summary>يُنادى مرّة واحدة لكل circuit قبل أوّل استخدام.</summary>
    Task InitAsync();

    /// <summary>تحوّل DateTime بـ UTC إلى توقيت المتصفّح.</summary>
    DateTime ToLocal(DateTime utc);

    /// <summary>تنسيق وقت مباشر مثل "14:30".</summary>
    string FormatTime(DateTime utc);

    /// <summary>تنسيق نسبيّ مثل "الآن"، "10د"، "3س"، "2ي"، أو تاريخ قديم.</summary>
    string FormatRelative(DateTime utc);
}

/// <summary>
/// Implementation لـ Blazor Server: تقرأ offset المتصفّح عبر JS interop
/// (window.ashareTz.offset/name) مرّة واحدة وتحفظه للدورة.
/// </summary>
public sealed class JsTimezoneProvider : ITimezoneProvider
{
    private readonly IJSRuntime _js;
    private int? _offsetMinutes;  // getTimezoneOffset: سالب لمن شرق UTC
    private string? _name;        // IANA string إن وُجد

    public JsTimezoneProvider(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        if (_offsetMinutes.HasValue) return;
        try
        {
            _offsetMinutes = await _js.InvokeAsync<int>("ashareTz.offset");
            _name          = await _js.InvokeAsync<string?>("ashareTz.name");
        }
        catch
        {
            // prerender أو بيئة اختبار — سقوط إلى توقيت الخادم.
            _offsetMinutes = (int)-DateTimeOffset.Now.Offset.TotalMinutes;
        }
    }

    public DateTime ToLocal(DateTime utc)
    {
        if (!_offsetMinutes.HasValue) return utc;
        return utc.AddMinutes(-_offsetMinutes.Value);
    }

    public string FormatTime(DateTime utc) => ToLocal(utc).ToString("HH:mm");

    public string FormatRelative(DateTime utc)
    {
        var local = ToLocal(utc);
        var now = _offsetMinutes.HasValue
            ? DateTime.UtcNow.AddMinutes(-_offsetMinutes.Value)
            : DateTime.UtcNow;
        var diff = now - local;
        if (diff.TotalSeconds < 45) return "الآن";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}د";
        if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours}س";
        if (diff.TotalDays    < 7)  return $"{(int)diff.TotalDays}ي";
        return local.ToString("yyyy-MM-dd");
    }
}
