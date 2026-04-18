using Microsoft.JSInterop;

namespace Ashare.V2.Web.Store;

/// <summary>
/// خدمة المنطقة الزمنيّة للمتصفّح.
///
/// الغرض: كلّ التواريخ تُخزَّن UTC في الخادم. عند العرض نحوّلها إلى توقيت
/// متصفّح المستخدم عبر JS interop (getTimezoneOffset) مرّة واحدة لكلّ دائرة Blazor.
///
/// الاستخدام:
///   @inject TimezoneService Tz
///   <span>@Tz.ToLocal(message.SentAt).ToString("HH:mm")</span>
///
/// ملاحظة: في ASP.NET/Blazor Server، `DateTime.ToLocalTime()` يستخدم توقيت
/// الخادم — لا المتصفّح — لذلك نحتاج offset المتصفّح.
/// </summary>
public class TimezoneService
{
    private readonly IJSRuntime _js;
    private int? _browserOffsetMinutes;  // الفرق بالدقائق — getTimezoneOffset سالب لمن غرب UTC
    private string? _browserTimeZone;    // IANA — قد لا يدعمها كلّ المتصفّحات

    public TimezoneService(IJSRuntime js) => _js = js;

    /// <summary>يُستدعى مرّة واحدة بعد أوّل render. الاستدعاءات التالية تعيد offset المحفوظ.</summary>
    public async Task InitAsync()
    {
        if (_browserOffsetMinutes.HasValue) return;
        try
        {
            _browserOffsetMinutes = await _js.InvokeAsync<int>("ashareTz.offset");
            _browserTimeZone      = await _js.InvokeAsync<string?>("ashareTz.name");
        }
        catch
        {
            // prerender أو بيئة اختبار — نسقط إلى توقيت الخادم.
            _browserOffsetMinutes = (int)-DateTimeOffset.Now.Offset.TotalMinutes;
        }
    }

    /// <summary>
    /// يحوّل DateTime مخزَّن بـ UTC إلى توقيت المتصفّح.
    /// لو لم يُستدعَ InitAsync بعد، يعود الوقت كما هو (UTC).
    /// </summary>
    public DateTime ToLocal(DateTime utc)
    {
        if (!_browserOffsetMinutes.HasValue) return utc;
        // getTimezoneOffset تعيد "UTC - local" بالدقائق، لذلك نطرح offset من utc.
        return utc.AddMinutes(-_browserOffsetMinutes.Value);
    }

    /// <summary>عرض موجز للوقت: 14:30</summary>
    public string FormatTime(DateTime utc) => ToLocal(utc).ToString("HH:mm");

    /// <summary>عرض نسبيّ: "الآن" / "10د" / "3س" / "2ي" / تاريخ إن أقدم.</summary>
    public string FormatRelative(DateTime utc)
    {
        var local = ToLocal(utc);
        var now   = _browserOffsetMinutes.HasValue
            ? DateTime.UtcNow.AddMinutes(-_browserOffsetMinutes.Value)
            : DateTime.UtcNow;
        var diff = now - local;
        if (diff.TotalSeconds < 45)   return "الآن";
        if (diff.TotalMinutes < 60)   return $"{(int)diff.TotalMinutes}د";
        if (diff.TotalHours   < 24)   return $"{(int)diff.TotalHours}س";
        if (diff.TotalDays    < 7)    return $"{(int)diff.TotalDays}ي";
        return local.ToString("yyyy-MM-dd");
    }

    public string? BrowserTimeZoneName => _browserTimeZone;
}
