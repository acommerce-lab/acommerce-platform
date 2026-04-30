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

            // اقرأ الإعداد من الجذر — إن لم يوجد نتجاهل بصمت.
            FirebaseClientConfig? cfg = null;
            try
            {
                var json = await _http.Client.GetStringAsync("/firebase-config.json", ct);
                cfg = JsonSerializer.Deserialize<FirebaseClientConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* لا يوجد ملف → لا fcm */ return; }

            if (cfg is null || string.IsNullOrWhiteSpace(cfg.ApiKey)
                            || string.IsNullOrWhiteSpace(cfg.AppId)
                            || string.IsNullOrWhiteSpace(cfg.MessagingSenderId))
                return;

            if (!_initialized)
            {
                var ok = await _js.InvokeAsync<bool>("ejarFirebase.init", ct, cfg);
                if (!ok) { _log.LogInformation("FCM init returned false; skipping."); return; }
                _initialized = true;
            }

            // VAPID public key — مطلوب للـ web push. لو فارغ نُسجِّل التهيئة
            // لاستلام foreground messages فقط (لا background subscription).
            if (string.IsNullOrWhiteSpace(cfg.VapidKey))
            {
                _log.LogInformation("FCM: vapidKey فارغ — رسائل foreground فقط، بدون اشتراك.");
                return;
            }

            var token = await _js.InvokeAsync<string?>("ejarFirebase.requestToken", ct, cfg.VapidKey);
            if (string.IsNullOrWhiteSpace(token))
            {
                _log.LogInformation("FCM: getToken أرجع null (الإذن مرفوض أو غير مدعوم).");
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
