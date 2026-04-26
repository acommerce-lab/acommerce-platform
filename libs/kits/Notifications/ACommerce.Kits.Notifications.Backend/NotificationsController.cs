using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Notifications.Backend;

/// <summary>
/// Inbox endpoints: list + mark-read + mark-all-read. App provides
/// <see cref="INotificationStore"/>; routes are role-agnostic — caller's
/// identity comes from the token.
/// </summary>
[ApiController]
[Authorize]
[Route("notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationStore _store;
    public NotificationsController(INotificationStore store) => _store = store;

    private string CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("user_id missing");

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _store.ListAsync(CallerId, ct);
        return this.OkEnvelope("notification.list", rows);
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(string id, CancellationToken ct)
    {
        var ok = await _store.MarkReadAsync(CallerId, id, ct);
        if (!ok) return this.NotFoundEnvelope("notification_not_found");
        return this.OkEnvelope("notification.read", new { id });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var n = await _store.MarkAllReadAsync(CallerId, ct);
        return this.OkEnvelope("notification.read.all", new { count = n });
    }
}
