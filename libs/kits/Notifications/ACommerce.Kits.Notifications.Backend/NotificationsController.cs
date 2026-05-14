using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Notifications.Backend;

/// <summary>
/// Inbox endpoints: list + mark-read + mark-all-read. App provides
/// <see cref="INotificationStore"/>; routes are role-agnostic — caller's
/// identity comes from the token.
///
/// <para>كُلّ مَسارات الكِتابَة (mark-read) تَمُرّ عَبر <see cref="OpEngine"/>
/// + <c>SaveAtEnd</c> — لا حِفظ مُباشِر في الـ store، تَدقيق كامِل لِكُلّ
/// تَغيير حالَة. F6 مُحَقَّق.</para>
/// </summary>
[ApiController]
[Authorize(Policy = NotificationsKitPolicies.Authenticated)]
[Route("notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationStore _store;
    private readonly OpEngine           _engine;
    public NotificationsController(INotificationStore store, OpEngine engine)
    {
        _store  = store;
        _engine = engine;
    }

    private string CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("user_id missing");

    private string CallerPartyId => $"User:{CallerId}";

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _store.ListAsync(CallerId, ct);
        return this.OkEnvelope("notification.list", rows);
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(string id, CancellationToken ct)
    {
        var ok = false;
        var op = Entry.Create("notification.read")
            .Describe($"User {CallerId} marks notification {id} as read")
            .From(CallerPartyId,            1, ("role", "reader"))
            .To($"Notification:{id}",       1, ("role", "marked_read"))
            .Tag("notification_id", id)
            .Execute(async ctx => ok = await _store.MarkReadAsync(CallerId, id, ctx.CancellationToken))
            .SaveAtEnd()
            .Build();
        var env = await _engine.ExecuteEnvelopeAsync(op, new { id }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "mark_read_failed",
                                           env.Operation.ErrorMessage);
        if (!ok) return this.NotFoundEnvelope("notification_not_found");
        return this.OkEnvelope("notification.read", new { id });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var n = 0;
        var op = Entry.Create("notification.read.all")
            .Describe($"User {CallerId} marks all notifications as read")
            .From(CallerPartyId,                  1, ("role", "reader"))
            .To($"Notifications:User:{CallerId}", 1, ("role", "marked_read"))
            .Execute(async ctx => n = await _store.MarkAllReadAsync(CallerId, ctx.CancellationToken))
            .SaveAtEnd()
            .Build();
        var env = await _engine.ExecuteEnvelopeAsync(op, new { count = n }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "mark_all_read_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("notification.read.all", new { count = n });
    }
}
