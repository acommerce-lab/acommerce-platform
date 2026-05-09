namespace ACommerce.Kits.Support.Frontend.Customer.Stores;

/// <summary>store reactive لتذاكر الدعم الفنّيّ للمستخدِم الحاليّ.</summary>
public interface ISupportStore
{
    IReadOnlyList<SupportTicketSummary> Tickets { get; }
    bool IsLoading { get; }
    event Action? Changed;

    Task LoadAsync(CancellationToken ct = default);
    Task<string> CreateAsync(string subject, string body, CancellationToken ct = default);
    Task ReplyAsync(string ticketId, string body, CancellationToken ct = default);
}

public sealed record SupportTicketSummary(
    string Id,
    string Subject,
    string Status,
    DateTime UpdatedAt,
    int UnreadReplies);
