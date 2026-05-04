using ACommerce.ClientHost.KitApi;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Interceptors;

/// <summary>
/// pre-flight يَفحص أنّ المستخدِم مُصادَق قبل أيّ kit api call إلا إذا كان
/// المسار <c>/auth/...</c> أو <c>/listings</c> العام (تَصَفّح بدون تَسجيل
/// دخول). يُمنَع النَفّاذ أحرفيّاً ⇒ analyzer يَردّ رسالة خطأ والـ
/// pipeline يَلتقطها في <see cref="KitApiResult{T}"/> دون أن يَصل HTTP.
///
/// <para>هذا يَضمن قاعدة واحدة لكلّ الكيتس بدون تَكرار <c>if (!Auth)</c>
/// في كلّ binding.</para>
/// </summary>
public sealed class RequiredAuthAnalyzer : IKitApiAnalyzer
{
    private readonly AppStore _app;
    public RequiredAuthAnalyzer(AppStore app) => _app = app;

    public string Name => "RequiredAuth";

    public Task<string?> CheckAsync(KitApiRequest request, CancellationToken ct)
    {
        // مَسارات عامّة لا تَتطلّب auth
        var path = request.Path;
        if (path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/plans",  StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/version",StringComparison.OrdinalIgnoreCase) ||
            (request.Method == "GET" && path.StartsWith("/listings", StringComparison.OrdinalIgnoreCase) && !path.Contains("/my-")))
            return Task.FromResult<string?>(null);

        return Task.FromResult(_app.Auth.IsAuthenticated ? null : "auth_required");
    }
}
