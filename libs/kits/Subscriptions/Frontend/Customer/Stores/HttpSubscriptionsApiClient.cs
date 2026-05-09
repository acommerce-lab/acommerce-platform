using ACommerce.ClientHost.KitApi;
using ACommerce.Subscriptions.Operations.Abstractions;

namespace ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;

/// <summary>
/// تنفيذ افتراضيّ يَستهلك <see cref="KitHttpClient"/>. DTOs تُحقّق
/// <see cref="IPlan"/>/<see cref="ISubscription"/> مباشرة (Law 6).
/// </summary>
public sealed class HttpSubscriptionsApiClient : ISubscriptionsApiClient
{
    private const string Kit = "subscriptions";
    private readonly KitHttpClient _http;

    public HttpSubscriptionsApiClient(KitHttpClient http) => _http = http;

    public async Task<IReadOnlyList<IPlan>> ListPlansAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync<List<PlanDto>>(Kit, "/plans", ct);
        return res.Success && res.Data is not null
            ? res.Data.Cast<IPlan>().ToList()
            : Array.Empty<IPlan>();
    }

    public async Task<ISubscription?> GetActiveAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync<SubscriptionDto>(Kit, "/me/subscription", ct);
        return res.Success ? res.Data : null;
    }

    public async Task<ISubscription?> ActivateAsync(string planId, CancellationToken ct = default)
    {
        var res = await _http.PostAsync<SubscriptionDto>(Kit, "/subscriptions/activate", new { planId }, ct);
        return res.Success ? res.Data : null;
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
        public Guid     Id        { get; set; }
        public Guid     UserId    { get; set; }
        public Guid     PlanId    { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate   { get; set; }
        public Dictionary<string, int> Used { get; set; } = new();
        public bool IsCurrentlyActive => DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;
        public DateTime  CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted     { get; set; }
    }
}
