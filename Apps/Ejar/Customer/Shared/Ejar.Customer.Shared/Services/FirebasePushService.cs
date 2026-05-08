using Ejar.Customer.UI.Interceptors;
using Ejar.Customer.UI.Store;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// جسر بين الواجهة و Firebase Cloud Messaging:
/// <list type="number">
///   <item>يقرأ <c>/firebase-config.json</c> من الجذر (يوفّره المضيف على
///         WebAssembly فقط حالياً — Server و MAUI لا يستهلكان FCM web push).</item>
///   <item>يستدعي <c>ejarFirebase.init(cfg)</c> لتهيئة SDK وتسجيل
///         <c>firebase-messaging-sw.js</c>.</item>
///   <item>يطلب <c>requestToken(vapidKey)</c> فيُرجع رمز جهاز فريد.</item>
///   <item>يرسله إلى الباك على <c>POST /me/push-subscription</c> ليُخزَّن
///         في جدول <c>UserPushTokens</c> عبر <c>EjarDeviceTokenStore</c>.</item>
/// </list>
///
/// يُستدعى من <c>Login</c> بعد نجاح OTP — التوكن متّاح أصلاً وتسجيل
/// الجهاز يحتاج هويّة المستخدم. لو الإعداد غير منشور (config فارغ أو
/// VAPID فارغ)، نتجاوز كل شيء بصمت.
/// </summary>
public sealed class FirebasePushService
{
    private readonly IJSRuntime _js;
    private readonly EjarCircuitHttp _http;
    private readonly AppStore _store;
    private readonly ILogger<FirebasePushService> _log;
    private bool _initialized;
    private FirebaseClientConfig? _cachedCfg;

    public FirebasePushService(
        IJSRuntime js,
        EjarCircuitHttp http,
        AppStore store,
        ILogger<FirebasePushService> log)
    {
        _js = js;
        _http = http;
        _store = store;
        _log = log;
    }

    /// <summary>
    /// تهيئة + تسجيل رمز الجهاز إذا لم يحدث بعد. آمن للاستدعاء المتكرّر.
    /// لا يفعل شيئاً إن لم يوجد <c>firebase-config.json</c> أو لم يتمّ
    /// تسجيل الدخول بعد.
    /// </summary>
    public async Task TryRegisterAsync(CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_store.Auth.AccessToken)) return;

            // اقرأ الإعداد + هيّئ SDK كلّه من JS على same-origin. الـ HttpClient
            // المحقون "ejar" BaseAddress = ejarapi.runasp.net، فلو طلبنا منه
            // /firebase-config.json يطلبه من الـ API بدل origin الواجهة (404).
            // ejarFirebase.initFromUrl يستخدم window.fetch فيلتزم same-origin.
            FirebaseClientConfig? cfg = null;
            if (!_initialized)
            {
                var raw = await _js.InvokeAsync<JsonElement>("ejarFirebase.initFromUrl", ct, "/firebase-config.json");
                if (raw.ValueKind != JsonValueKind.Object)
                {
                    _log.LogInformation("FCM: تجاهل — initFromUrl أرجع false (ملف الإعداد غير موجود أو غير مدعوم).");
                    return;
                }
                cfg = raw.Deserialize<FirebaseClientConfig>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cfg is null) return;
                _cachedCfg = cfg;
                _initialized = true;
            }
            else
            {
                cfg = _cachedCfg;
            }
            if (cfg is null) return;

            // VAPID public key — لو فارغ أو غير صالح (الطول ≠ 87-88 ولا يَبدأ
            // بـ B) نَخرج بـ error واضح. السبب الأكثر شيوعاً لفشل الـ push:
            // مفتاح خاطئ ⇒ DOMException 'applicationServerKey is not valid'.
            if (string.IsNullOrWhiteSpace(cfg.VapidKey)
                || cfg.VapidKey.Length < 80 || cfg.VapidKey.Length > 100
                || cfg.VapidKey[0] != 'B')
            {
                _log.LogError(
                    "FCM: VAPID key غير صالح (الطول={Len}). افتح Firebase Console → " +
                    "Project Settings → Cloud Messaging → Web Push certificates → " +
                    "Generate key pair → Public key (88 char يَبدأ بـ B) → ضعه في " +
                    "wwwroot/firebase-config.json تحت \"vapidKey\".",
                    cfg.VapidKey?.Length ?? 0);
                return;
            }

            var token = await _js.InvokeAsync<string?>("ejarFirebase.requestToken", ct, cfg.VapidKey);
            if (string.IsNullOrWhiteSpace(token))
            {
                _log.LogWarning("FCM: getToken أرجع null. راجع DevTools Console — " +
                    "غالباً VAPID مَرفوض أو إذن الإشعارات مَمنوع.");
                return;
            }

            // أرسل الرمز للباك. AuthHeadersHandler يحقن Bearer.
            try
            {
                var resp = await _http.Client.PostAsJsonAsync("/me/push-subscription",
                    new { token, platform = "web" }, ct);
                if (!resp.IsSuccessStatusCode)
                    _log.LogWarning("FCM register failed: {Status}", (int)resp.StatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "FCM register call failed");
            }
        }
        catch (Exception ex)
        {
            // لا نُفشل الـ login بسبب push — كل المسارات الأخرى مستقلّة.
            _log.LogDebug(ex, "FirebasePushService.TryRegisterAsync غير قاتل");
        }
    }

    public sealed class FirebaseClientConfig
    {
        public string? ApiKey { get; set; }
        public string? AuthDomain { get; set; }
        public string? ProjectId { get; set; }
        public string? StorageBucket { get; set; }
        public string? MessagingSenderId { get; set; }
        public string? AppId { get; set; }
        public string? MeasurementId { get; set; }
        public string? VapidKey { get; set; }
    }
}
