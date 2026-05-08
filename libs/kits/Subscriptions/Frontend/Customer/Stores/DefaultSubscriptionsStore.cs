using ACommerce.Client.Operations;
using ACommerce.Subscriptions.Operations.Abstractions;

namespace ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;

/// <summary>OAM-shaped (F61) — subscription.* operations عَبر ITemplateEngine.</summary>
public sealed class DefaultSubscriptionsStore : ISubscriptionsStore
{
    private readonly ITemplateEngine _engine;
    private List<IPlan> _plans = new();
    public DefaultSubscriptionsStore(ITemplateEngine engine) => _engine = engine;

    public IReadOnlyList<IPlan> Plans => _plans;
    public ISubscription? Active { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadPlansAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<List<PlanDto>>(SubscriptionsOps.ListPlans(), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _plans = env.Data.Cast<IPlan>().ToList();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task LoadActiveAsync(CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<SubscriptionDto>(SubscriptionsOps.GetActive(), ct: ct);
        Active = env.Operation.Status == "Success" ? env.Data : null;
        Changed?.Invoke();
    }

    public async Task SubscribeAsync(string planId, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<SubscriptionDto>(
            SubscriptionsOps.Activate(planId), payload: new { planId }, ct: ct);
        if (env.Operation.Status == "Success" && env.Data is not null)
            Active = env.Data;
        Changed?.Invoke();
    }

    private sealed class PlanDto : IPlan
    {
        public Guid   Id            { get; set; }
        public string Slug          { get; set; } = "";
        public string Name          { get; set; } = "";
        public bool   IsActive      { get; set; }
        public Dictionary<string, int>    Quotas        { get; set; } = new();
        public Dictionary<string, string> AllowedScopes { get; set; } = new();
        public DateTime  CreatedAt  { get; set; }
        public DateTime? UpdatedAt  { get; set; }
        public bool IsDeleted       { get; set; }
    }

    private sealed class SubscriptionDto : ISubscription
    {
        public Guid     Id          { get; set; }
        public Guid     UserId      { get; set; }
        public Guid     PlanId      { get; set; }
        public DateTime StartDate   { get; set; }
        public DateTime EndDate     { get; set; }
        public Dictionary<string, int> Used { get; set; } = new();
        public DateTime  CreatedAt  { get; set; }
        public DateTime? UpdatedAt  { get; set; }
        public bool      IsDeleted  { get; set; }
        public bool      IsCurrentlyActive
            => !IsDeleted && DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;
    }
}
