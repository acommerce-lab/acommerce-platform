using ACommerce.Notification.Providers.Firebase.Storage;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Notifications.Backend;

/// <summary>
/// تسجيل/إلغاء رموز الأجهزة لإشعارات الـ push (FCM web/Android/iOS).
/// مسارات:
///   <c>POST   /me/push-subscription</c>          — تَسجيل رمز.
///   <c>DELETE /me/push-subscription/{token}</c>  — إلغاء.
///
/// <para>الـ controller يَستهلك <see cref="IDeviceTokenStore"/> الذي يَأتي
/// من Firebase provider. لو الـ FCM غير مهيّأ (لا creds)، الـ
/// <c>IDeviceTokenStore</c> يَكون null ⇒ نَردّ 200 ok بصمت ليَبقى الفرونت
/// غير مُلزَم بحالة التهيئة على الخادم.</para>
/// </summary>
[ApiController]
[Authorize(Policy = NotificationsKitPolicies.Authenticated)]
public sealed class DeviceTokensController : ControllerBase
{
    private readonly IDeviceTokenStore? _store;
    public DeviceTokensController(IDeviceTokenStore? store = null) => _store = store;

    private string? CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public sealed record SubscribeBody(string? Token, string? Platform);

    [HttpPost("/me/push-subscription")]
    public async Task<IActionResult> Register([FromBody] SubscribeBody body, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body?.Token))
            return this.BadRequestEnvelope("missing_token");

        if (_store is null)
            return this.OkEnvelope("push.subscribe", new { ok = true, fcmConfigured = false });

        await _store.RegisterAsync(CallerId, body.Token!, body.Platform, ct);
        return this.OkEnvelope("push.subscribe", new { ok = true, fcmConfigured = true });
    }

    [HttpDelete("/me/push-subscription/{token}")]
    public async Task<IActionResult> Unregister(string token, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (_store is not null) await _store.UnregisterAsync(token, ct);
        return this.OkEnvelope("push.unsubscribe", new { ok = true });
    }
}
