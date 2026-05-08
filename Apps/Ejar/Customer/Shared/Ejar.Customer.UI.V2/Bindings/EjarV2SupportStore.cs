using ACommerce.Kits.Support.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.V2.Bindings;

public sealed class EjarV2SupportStore : ISupportStore
{
    private readonly ISupportApiClient _api;
    private List<SupportTicketSummary> _tickets = new();
    public EjarV2SupportStore(ISupportApiClient api) => _api = api;

    public IReadOnlyList<SupportTicketSummary> Tickets => _tickets;
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try   { _tickets = (await _api.ListAsync(ct)).ToList(); }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task<string> CreateAsync(string subject, string body, CancellationToken ct = default)
    {
        var id = await _api.CreateAsync(subject, body, ct);
        if (!string.IsNullOrEmpty(id)) await LoadAsync(ct);
        return id;
    }

    public async Task ReplyAsync(string ticketId, string body, CancellationToken ct = default)
    {
        await _api.ReplyAsync(ticketId, body, ct);
        Changed?.Invoke();
    }
}
