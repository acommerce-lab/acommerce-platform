using ACommerce.Kits.Support.Frontend.Customer.Stores;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="ISupportStore"/> لإيجار. يَستهلك
/// <c>GET /support/tickets</c> + <c>POST /support/tickets</c> +
/// <c>POST /support/tickets/{id}/replies</c>.
/// </summary>
public sealed class EjarSupportStore : ISupportStore
{
    private readonly ApiReader _api;
    private List<SupportTicketSummary> _tickets = new();

    public EjarSupportStore(ApiReader api) => _api = api;

    public IReadOnlyList<SupportTicketSummary> Tickets => _tickets;
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _api.GetAsync<List<SupportTicketSummary>>("/support/tickets", ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _tickets = env.Data;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task<string> CreateAsync(string subject, string body, CancellationToken ct = default)
    {
        var env = await _api.PostAsync<CreatedTicket>("/support/tickets", new { subject, body }, ct);
        if (env.Operation.Status != "Success" || env.Data is null) return string.Empty;
        await LoadAsync(ct);
        return env.Data.Id;
    }

    public async Task ReplyAsync(string ticketId, string body, CancellationToken ct = default)
    {
        await _api.PostAsync<object>($"/support/tickets/{Uri.EscapeDataString(ticketId)}/replies", new { body }, ct);
        Changed?.Invoke();
    }

    private sealed record CreatedTicket(string Id);
}
