namespace ACommerce.Kits.Support.Frontend.Customer.Stores;

/// <summary>
/// عميل HTTP خاصّ بـ Support kit. يَعرف <c>SupportController</c>:
/// <list type="bullet">
///   <item><c>GET /support/tickets</c> ⇒ <c>SupportTicketSummary[]</c></item>
///   <item><c>POST /support/tickets</c> ⇒ <c>{ id }</c></item>
///   <item><c>POST /support/tickets/{id}/replies</c></item>
/// </list>
/// </summary>
public interface ISupportApiClient
{
    Task<IReadOnlyList<SupportTicketSummary>> ListAsync(CancellationToken ct = default);
    Task<string> CreateAsync(string subject, string body, CancellationToken ct = default);
    Task<bool>   ReplyAsync(string ticketId, string body, CancellationToken ct = default);
}
