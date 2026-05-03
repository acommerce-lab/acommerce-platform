using ACommerce.Kits.Support.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Bindings;

public sealed class EjarSupportStore : ISupportStore
{
    public IReadOnlyList<SupportTicketSummary> Tickets { get; private set; } = Array.Empty<SupportTicketSummary>();
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public Task LoadAsync(CancellationToken ct = default)                                              { Changed?.Invoke(); return Task.CompletedTask; }
    public Task<string> CreateAsync(string subject, string body, CancellationToken ct = default)       => Task.FromResult(Guid.NewGuid().ToString());
    public Task ReplyAsync(string ticketId, string body, CancellationToken ct = default)               { Changed?.Invoke(); return Task.CompletedTask; }
}
