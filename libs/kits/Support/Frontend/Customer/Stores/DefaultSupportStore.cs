using ACommerce.Client.Operations;

namespace ACommerce.Kits.Support.Frontend.Customer.Stores;

/// <summary>OAM-shaped (F61).</summary>
public sealed class DefaultSupportStore : ISupportStore
{
    private readonly ITemplateEngine _engine;
    private List<SupportTicketSummary> _tickets = new();
    public DefaultSupportStore(ITemplateEngine engine) => _engine = engine;

    public IReadOnlyList<SupportTicketSummary> Tickets => _tickets;
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<List<SupportTicketSummary>>(SupportOps.ListTickets(), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _tickets = env.Data;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task<string> CreateAsync(string subject, string body, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<TicketCreatedDto>(
            SupportOps.CreateTicket(subject),
            payload: new { subject, body },
            ct: ct);
        var id = env.Operation.Status == "Success" ? env.Data?.Id ?? "" : "";
        if (!string.IsNullOrEmpty(id)) await LoadAsync(ct);
        return id;
    }

    public async Task ReplyAsync(string ticketId, string body, CancellationToken ct = default)
    {
        await _engine.ExecuteAsync<object>(SupportOps.Reply(ticketId), payload: new { body }, ct: ct);
        Changed?.Invoke();
    }

    private sealed record TicketCreatedDto(string Id);
}
