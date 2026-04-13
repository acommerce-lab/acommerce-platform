using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Ashare.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Subscription> _subs;
    private readonly IBaseAsyncRepository<Plan> _plans;
    private readonly OpEngine _engine;

    public SubscriptionsController(
        IRepositoryFactory factory,
        OpEngine engine)
    {
        _subs = factory.CreateRepository<Subscription>();
        _plans = factory.CreateRepository<Plan>();
        _engine = engine;
    }

    public record SubscribeRequest(Guid UserId, Guid PlanId, string BillingCycle);

    /// <summary>
    /// إنشاء اشتراك - يُمثل كقيد محاسبي:
    /// المستخدم (مدين) ← المنصة (دائن) بقيمة الاشتراك.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req, CancellationToken ct)
    {
        var plan = await _plans.GetByIdAsync(req.PlanId, ct);
        if (plan == null) return this.NotFoundEnvelope("plan_not_found");
        if (!plan.IsActive) return this.BadRequestEnvelope("plan_inactive");

        var amount = plan.GetPrice(req.BillingCycle);

        var now = DateTime.UtcNow;
        DateTime endDate = req.BillingCycle.ToLowerInvariant() switch
        {
            "annual"     => now.AddYears(1),
            "semiannual" => now.AddMonths(6),
            "quarterly"  => now.AddMonths(3),
            _            => now.AddMonths(1)
        };

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            UserId = req.UserId,
            PlanId = plan.Id,
            BillingCycle = req.BillingCycle,
            StartDate = now,
            EndDate = endDate,
            TrialEndDate = plan.TrialDays > 0 ? now.AddDays(plan.TrialDays) : null,
            Status = plan.TrialDays > 0 ? SubscriptionStatus.Trial : SubscriptionStatus.Pending,
            AmountPaid = amount,
            Currency = plan.Currency
        };

        var op = Entry.Create("subscription.create")
            .Describe($"User:{req.UserId} subscribes to {plan.Slug} ({req.BillingCycle})")
            .From($"User:{req.UserId}", amount,
                ("role", "subscriber"),
                ("subscription_status", "pending"))
            .To($"Plan:{plan.Slug}", amount,
                ("role", "plan"),
                ("plan_id", plan.Id.ToString()))
            .Tag("subscription_id", subscription.Id.ToString())
            .Tag("billing_cycle", req.BillingCycle)
            .Tag("currency", plan.Currency)
            // محلل: المبلغ غير سالب
            .Analyze(new RangeAnalyzer("amount", () => amount, min: 0))
            .Execute(async ctx =>
            {
                await _subs.AddAsync(subscription, ctx.CancellationToken);
                ctx.Set("subscriptionId", subscription.Id);
            })
            .OnAfterComplete(async ctx =>
            {
                // إذا كانت تجريبية تكون نشطة فوراً، وإلا تظل Pending حتى الدفع
                if (plan.TrialDays > 0 || plan.MonthlyPrice == 0)
                {
                    subscription.Status = SubscriptionStatus.Active;
                    subscription.OperationId = ctx.Operation.Id;
                    await _subs.UpdateAsync(subscription, ctx.CancellationToken);
                }
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, subscription, ct);

        if (envelope.Operation.Status != "Success")
            return this.BadRequestEnvelope(envelope.Operation.FailedAnalyzer ?? "subscription_create_failed", envelope.Operation.ErrorMessage);

        envelope.Meta = new Dictionary<string, object>
        {
            ["requiresPayment"] = subscription.Status == SubscriptionStatus.Pending,
            ["amount"] = amount,
            ["plan"] = new { plan.Id, plan.Slug, plan.Name }
        };

        return Created($"/api/subscriptions/{subscription.Id}", envelope);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var s = await _subs.GetByIdAsync(id, ct);
        return s == null ? this.NotFoundEnvelope("subscription_not_found") : this.OkEnvelope("subscription.get", s);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> ByUser(Guid userId, CancellationToken ct)
    {
        var list = await _subs.GetAllWithPredicateAsync(s => s.UserId == userId);
        return this.OkEnvelope("subscription.list", list.OrderByDescending(s => s.StartDate).ToList());
    }

    [HttpGet("user/{userId:guid}/active")]
    public async Task<IActionResult> ActiveByUser(Guid userId, CancellationToken ct)
    {
        var subs = await _subs.GetAllWithPredicateAsync(s =>
            s.UserId == userId &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial));

        var sub = subs.OrderBy(s => s.StartDate).FirstOrDefault(s => s.IsCurrentlyActive);
        if (sub == null) return this.NotFoundEnvelope("no_active_subscription");

        var plan = await _plans.GetByIdAsync(sub.PlanId, ct);
        return this.OkEnvelope("subscription.active", new { subscription = sub, plan });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var sub = await _subs.GetByIdAsync(id, ct);
        if (sub == null) return this.NotFoundEnvelope("subscription_not_found");

        var plan = await _plans.GetByIdAsync(sub.PlanId, ct);

        var op = Entry.Create("subscription.cancel")
            .Describe($"Cancel subscription #{sub.Id} for User:{sub.UserId} — reversal from Plan:{sub.PlanId}")
            .From($"Plan:{sub.PlanId}", sub.AmountPaid, ("role", "plan"))
            .To($"User:{sub.UserId}", sub.AmountPaid, ("role", "subscriber"))
            .Tag("subscription_id", sub.Id.ToString())
            .Tag("plan_id", sub.PlanId.ToString())
            .Tag("user_id", sub.UserId.ToString())
            .Execute(async ctx =>
            {
                sub.Status = SubscriptionStatus.Cancelled;
                sub.EndDate = DateTime.UtcNow;
                await _subs.UpdateAsync(sub, ctx.CancellationToken);
                ctx.Set("subscriptionId", sub.Id);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("subscription_cancel_failed", result.ErrorMessage);

        return this.OkEnvelope("subscription.cancel", sub);
    }
}
