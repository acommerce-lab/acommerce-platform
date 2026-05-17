using Microsoft.JSInterop;

namespace ACommerce.Kits.Versions.Templates;

/// <summary>
/// تتبّع توفّر إصدار أحدث أثناء تشغيل التطبيق. الـ <c>version-check.js</c>
/// يفحص <c>version.json</c> مرّة عند بدء التحميل ويُجبر تنظيف SW + reload
/// لو اختلف الإصدار. لكنّ المستخدم قد يُبقي التبويب مفتوحاً ساعات بينما
/// نشرنا إصداراً جديداً — هنا يأتي دور هذا الـ poller:
/// <list type="number">
///   <item>كلّ <see cref="PollInterval"/> ثانية: fetch <c>/version.json?bust=now</c></item>
///   <item>قارن مع نسخة العميل المحلّيّة (<see cref="AppVersionInfo.Version"/>).</item>
///   <item>لو مختلفة — اضبط <see cref="UpdateAvailable"/> = true وأطلق <see cref="Changed"/>.</item>
/// </list>
/// MainLayout يعرض بانر "إصدار جديد متاح" مع زرّ "تحديث الآن" يستدعي
/// <see cref="ApplyUpdateAsync"/> الذي ينظّف SW + caches ويُعيد التحميل.
/// </summary>
public sealed class VersionPoll : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly AppVersionInfo _info;
    private readonly HttpClient _http;
    private CancellationTokenSource? _cts;

    public VersionPoll(IJSRuntime js, AppVersionInfo info, IHttpClientFactory httpFactory)
    {
        _js   = js;
        _info = info;
        // HttpClient عاديّ بلا أيّ handlers — version.json منشور كـ static
        // مع الواجهة (نفس origin)، لا يحتاج auth ولا culture headers.
        _http = httpFactory.CreateClient();
    }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    public bool UpdateAvailable { get; private set; }
    public string? AvailableVersion { get; private set; }
    public event Action? Changed;

    public Task StartAsync()
    {
        if (_cts is not null) return Task.CompletedTask;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        // فحص فوريّ بعد ثانيتَين فقط — كان ٥ ثوانٍ، يُربك المستخدم: ينشر
        // تحديثاً ولا يرى البانر إلاّ بعد دقيقة كاملة. الآن: ٢ث + ٣٠ث.
        try { await Task.Delay(TimeSpan.FromSeconds(2), ct); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try { await CheckOnceAsync(ct); }
            catch { /* تجاهل أيّ فشل شبكيّ — نحاول لاحقاً */ }
            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task CheckOnceAsync(CancellationToken ct)
    {
        // إذا كان _info.Version لا يزال على الـ fallback "1.0.0" فهذا يعني
        // أنّ appsettings.json لم تُحمَّل بعد (تكون الـ Configuration ما زالت
        // تُجلَب أو الـ Service Worker قدّم نسخة مكسورة). بدل أن نُطلِق إنذاراً
        // كاذباً ("نسخة جديدة!" بينما الواقع أنّ AppVersionInfo فاسد) نتخطّى
        // الفحص ونحاول لاحقاً.
        if (_info.Version is null or "" or "1.0.0") return;

        var url = $"version.json?bust={DateTime.UtcNow.Ticks}";
        try
        {
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return;
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("version", out var v)) return;
            var server = v.GetString();
            if (string.IsNullOrEmpty(server)) return;
            if (server == _info.Version) return;

            UpdateAvailable  = true;
            AvailableVersion = server;
            Changed?.Invoke();
        }
        catch { /* صامت */ }
    }

    /// <summary>
    /// ينظّف SW + caches المتصفّح ثمّ يُعيد التحميل بـ ?ac_v=<version> ليكسر
    /// أيّ HTTP cache على CDN/proxy. نفس منطق version-check.js لكن المستخدم
    /// يُشغّله بضغطة زرّ.
    ///
    /// <para>السابِق: <c>eval(...)</c> — يُمنَع تَحت CSP الافتراضيّ لِـ
    /// Blazor WASM (<c>'wasm-unsafe-eval'</c> دون <c>'unsafe-eval'</c>) فَلا
    /// تَعمَل النَّقرَة على زَرّ "تَحديث الآن". الجَديد: نَستَدعي
    /// <c>window.acVersionRefresh</c> دالّة عامّة مُعَرَّفَة في
    /// <c>pwa-update.js</c> — اسم دالّة عاديّ بِلا eval فَيَمُرّ تَحت CSP
    /// الصارِم. التَّطبيقات الَّتي تَستَعمِل الـ kit يَجِب أَن تَنشُر
    /// <c>pwa-update.js</c> (أو تَعرِض <c>acVersionRefresh</c> بِأيّ طَريقة
    /// أُخرى) — وإلاّ نَسقُط إلى <c>location.reload</c>.</para>
    /// </summary>
    public async Task ApplyUpdateAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("acVersionRefresh");
            return;
        }
        catch
        {
            // غير مَعروفَة أو فَشلَت — أعِد التَّحميل عَلى الأَقَلّ.
        }
        try { await _js.InvokeVoidAsync("location.reload"); }
        catch { /* last resort — لا شَيء يُمكِن فِعله */ }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return ValueTask.CompletedTask;
    }
}
