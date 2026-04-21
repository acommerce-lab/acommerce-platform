using Microsoft.JSInterop;

namespace Ejar.Web.Store;

public interface ITimezoneProvider
{
    Task InitAsync();
    DateTime ToLocal(DateTime utc);
    string FormatTime(DateTime utc);
    string FormatRelative(DateTime utc);
}

public sealed class JsTimezoneProvider : ITimezoneProvider
{
    private readonly IJSRuntime _js;
    private int? _offsetMinutes;
    private string? _name;

    public JsTimezoneProvider(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        if (_offsetMinutes.HasValue) return;
        try
        {
            _offsetMinutes = await _js.InvokeAsync<int>("ejarTz.offset");
            _name          = await _js.InvokeAsync<string?>("ejarTz.name");
        }
        catch
        {
            _offsetMinutes = (int)-DateTimeOffset.Now.Offset.TotalMinutes;
        }
    }

    public DateTime ToLocal(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Local) return dt;
        if (!_offsetMinutes.HasValue) return dt;
        return DateTime.SpecifyKind(dt.AddMinutes(-_offsetMinutes.Value), DateTimeKind.Local);
    }

    public string FormatTime(DateTime dt) => ToLocal(dt).ToString("HH:mm");

    public string FormatRelative(DateTime dt)
    {
        var local = ToLocal(dt);
        var now = _offsetMinutes.HasValue
            ? DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-_offsetMinutes.Value), DateTimeKind.Local)
            : DateTime.Now;
        var diff = now - local;
        if (diff.TotalSeconds < 45) return "الآن";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}د";
        if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours}س";
        if (diff.TotalDays    < 7)  return $"{(int)diff.TotalDays}ي";
        return local.ToString("yyyy-MM-dd");
    }
}
