using ACommerce.ClientHost.KitApi;

namespace ACommerce.Kits.Support.Frontend.Customer.Stores;

public sealed class HttpSupportApiClient : ISupportApiClient
{
    private const string Kit = "support";
    private readonly KitHttpClient _http;

    public HttpSupportApiClient(KitHttpClient http) => _http = http;

    public async Task<IReadOnlyList<SupportTicketSummary>> ListAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync<List<SupportTicketSummary>>(Kit, "/support/tickets", ct);
        return res.Success && res.Data is not null ? res.Data : Array.Empty<SupportTicketSummary>();
    }

    public async Task<string> CreateAsync(string subject, string body, CancellationToken ct = default)
    {
        var res = await _http.PostAsync<CreatedTicket>(Kit, "/support/tickets", new { subject, body }, ct);
        return res.Success && res.Data is not null ? res.Data.Id : string.Empty;
    }

    public async Task<bool> ReplyAsync(string ticketId, string body, CancellationToken ct = default)
    {
        var res = await _http.PostAsync<object>(Kit,
            $"/support/tickets/{Uri.EscapeDataString(ticketId)}/replies", new { body }, ct);
        return res.Success;
    }

    private sealed record CreatedTicket(string Id);
}
