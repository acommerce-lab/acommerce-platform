using ACommerce.Kits.Versions.Templates;
using Microsoft.JSInterop;

namespace Ejar.Customer.UI.Services;

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

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(2);

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
        // فحص أوّل بعد ٣٠ ثانية من الإقلاع (لتجنّب race مع version-check.js
        // الأوّليّ على نفس التحميل)، ثمّ كلّ PollInterval.
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
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
        // مسار نسبيّ — IHttpClientFactory.CreateClient() يحلّ على origin
        // الواجهة (للـ WASM = أصل التطبيق، لـ Server = host). bust=now
        // يكسر أيّ HTTP cache من المتصفّح/CDN.
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
    /// </summary>
    public async Task ApplyUpdateAsync()
    {
        try { await _js.InvokeVoidAsync("eval", @"(async()=>{
            try {
              if ('serviceWorker' in navigator) {
                const regs = await navigator.serviceWorker.getRegistrations();
                await Promise.all(regs.map(r => r.unregister().catch(()=>{})));
              }
              if ('caches' in window) {
                const keys = await caches.keys();
                await Promise.all(keys.map(k => caches.delete(k).catch(()=>{})));
              }
            } catch (e) { console.warn('[VersionPoll] cleanup failed', e); }
            const u = new URL(location.href);
            u.searchParams.set('ac_v', Date.now().toString());
            location.replace(u.toString());
        })()"); }
        catch { /* لو JS فشل، إعادة تحميل بسيطة كحلّ احتياط */ }
        try { await _js.InvokeVoidAsync("eval", "location.reload()"); } catch { }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return ValueTask.CompletedTask;
    }
}
