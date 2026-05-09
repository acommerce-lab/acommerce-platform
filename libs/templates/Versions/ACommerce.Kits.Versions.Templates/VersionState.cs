using System.Net.Http.Json;
using ACommerce.Kits.Versions.Operations;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.Kits.Versions.Templates;

/// <summary>
/// حامل حالة الإصدار للواجهة الأماميّة. Singleton (web server) أو scoped (WASM).
/// التطبيق يحقنه ويستهلكه عبر <see cref="AcVersionGate"/>.
/// </summary>
public sealed class VersionState
{
    public VersionCheckResult? Last { get; private set; }
    public bool Loaded { get; private set; }
    public event Action? Changed;

    private readonly IHttpClientFactory _http;
    private readonly AppVersionInfo _info;
    private readonly string _clientName;

    public VersionState(IHttpClientFactory http, AppVersionInfo info, VersionStateOptions opts)
    {
        _http = http;
        _info = info;
        _clientName = opts.HttpClientName;
    }

    /// <summary>هل التطبيق محجوب الآن (Status == Unsupported)؟</summary>
    public bool IsBlocked => Last?.IsBlocked == true;

    /// <summary>هل يجب إظهار شريط تحذير ناعم (NearSunset / Deprecated)؟</summary>
    public bool ShouldWarn => Last?.ShouldWarn == true;

    /// <summary>هل هناك إصدار أحدث متاح؟</summary>
    public bool HasNewer => Last?.HasNewer == true;

    /// <summary>
    /// يستدعي <c>GET /version/check</c> ويخزّن النتيجة. آمن للاستدعاء أكثر من مرّة
    /// (يبقي آخر نتيجة). الفشل الشبكيّ لا يحجب التطبيق — فقط يبقي الحالة فارغة.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _http.CreateClient(_clientName);
            var url = $"version/check?platform={Uri.EscapeDataString(_info.Platform)}" +
                      $"&version={Uri.EscapeDataString(_info.Version)}";
            var env = await client.GetFromJsonAsync<OperationEnvelope<VersionCheckResult>>(url, ct);
            Last = env?.Data;
        }
        catch
        {
            // فشل الشبكة — لا نحجب التطبيق على غياب الفحص.
            Last = null;
        }
        finally
        {
            Loaded = true;
            Changed?.Invoke();
        }
    }
}

public sealed class VersionStateOptions
{
    /// <summary>اسم الـ HttpClient المسجّل في DI الذي يضرب الخدمة الخلفيّة.</summary>
    public string HttpClientName { get; init; } = "default";
}
