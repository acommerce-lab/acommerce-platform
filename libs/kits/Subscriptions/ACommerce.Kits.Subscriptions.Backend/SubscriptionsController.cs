using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.Subscriptions.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Subscriptions.Backend;

/// <summary>
/// نقاط نهاية الاشتراك الذاتيّة. مسارات:
///   <c>GET  /me/subscription</c>      — الاشتراك النشط للمستخدم.
///   <c>POST /subscriptions/activate</c> — تفعيل باقة.
///
/// <para>وضع التجربة المفتوحة (<see cref="SubscriptionsKitOptions.OpenAccess"/>):
/// عند تفعيله، <c>GET /me/subscription</c> يردّ اشتراكاً اصطناعيّاً نشطاً
/// دون استدعاء الـ store. مفيد للإطلاق التجريبيّ بدون نظام دفع، ومفيد
/// لتعطيل بوّابات الحصص في كلّ القيود التي تشترط اشتراكاً نشطاً.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionStore _store;
    private readonly OpEngine _engine;
    private readonly SubscriptionsKitOptions _options;

    public SubscriptionsController(
        ISubscriptionStore store, OpEngine engine, SubscriptionsKitOptions options)
    {
        _store = store; _engine = engine; _options = options;
    }

    private string? CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("/me/subscription")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();

        if (_options.OpenAccess)
            return this.OkEnvelope("me.subscription", BuildTrialSubscription());

        var sub = await _store.GetActiveAsync(CallerId, ct);
        return this.OkEnvelope<object?>("me.subscription", sub);
    }

    public sealed record ActivateBody(string? PlanId);

    [HttpPost("/subscriptions/activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateBody req, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(req?.PlanId))
            return this.BadRequestEnvelope("missing_plan_id");

        SubscriptionView? activated = null;
        var op = Entry.Create("subscription.activate")
            .Describe($"User {CallerId} activates plan {req.PlanId}")
            .From($"User:{CallerId}", 1, ("role", "subscriber"))
            .To($"Plan:{req.PlanId}", 1, ("role", "activated"))
            .Tag("plan_id", req.PlanId)
            // المعترض الـ general للحصص يتجاوز كلّ subscription.* تلقائيّاً
            // لكن نضع الـ tag صراحةً لو طبَّق التطبيق سياسات أخرى.
            .Tag(SubscriptionTagKeys.SkipSubscriptionGate, "true")
            .Execute(async ctx =>
            {
                activated = await _store.ActivateAsync(CallerId!, req.PlanId!, ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (object?)activated ?? new { }, ct);
        if (env.Operation.Status != "Success" || activated is null)
            return this.BadRequestEnvelope(
                env.Operation.FailedAnalyzer ?? "activate_failed",
                env.Operation.ErrorMessage);

        return this.OkEnvelope("subscription.activate", activated);
    }

    private SubscriptionView BuildTrialSubscription()
    {
        var start = DateTime.UtcNow.Date;
        var years = Math.Max(1, _options.TrialDurationYears);
        return new SubscriptionView(
            Id:               Guid.Empty.ToString(),
            PlanId:           Guid.Empty.ToString(),
            PlanName:         _options.TrialPlanName,
            Status:           "active",
            StartDate:        start,
            EndDate:          start.AddYears(years),
            ListingsLimit:    0,    // 0 = unlimited (لا حصّة)
            FeaturedLimit:    0,
            ImagesPerListing: 0,
            Price:            0m,
            DaysRemaining:    years * 365,
            ListingsUsed:     0,
            FeaturedUsed:     0);
    }
}
