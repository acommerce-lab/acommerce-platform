using ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;
using ACommerce.Subscriptions.Operations.Abstractions;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="ISubscriptionsStore"/> لإيجار. يَجلب الخطط من
/// <c>GET /plans</c> والاشتراك النَّشِط من <c>GET /me/subscription</c>،
/// ويَدفع subscribe بـ <c>POST /me/subscription</c>.
/// </summary>
public sealed class EjarSubscriptionsStore : ISubscriptionsStore
{
    private readonly ApiReader _api;
    private List<IPlan> _plans = new();

    public EjarSubscriptionsStore(ApiReader api) => _api = api;

    public IReadOnlyList<IPlan> Plans => _plans;
    public ISubscription? Active { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadPlansAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _api.GetAsync<List<PlanDto>>("/plans", ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _plans = env.Data.Cast<IPlan>().ToList();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task LoadActiveAsync(CancellationToken ct = default)
    {
        var env = await _api.GetAsync<SubscriptionDto>("/me/subscription", ct: ct);
        if (env.Operation.Status == "Success" && env.Data is not null)
            Active = env.Data;
        Changed?.Invoke();
    }

    public async Task SubscribeAsync(string planId, CancellationToken ct = default)
    {
        var env = await _api.PostAsync<SubscriptionDto>("/me/subscription", new { planId }, ct);
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
        public DateTime CreatedAt   { get; set; }
        public DateTime? UpdatedAt  { get; set; }
        public bool IsDeleted       { get; set; }
    }

    private sealed class SubscriptionDto : ISubscription
    {
        public Guid     Id        { get; set; }
        public Guid     UserId    { get; set; }
        public Guid     PlanId    { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate   { get; set; }
        public Dictionary<string, int> Used { get; set; } = new();
        public bool IsCurrentlyActive => DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted     { get; set; }
    }
}
