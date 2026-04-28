using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Support.Operations;

public static class SupportOperations
{
    public const string FileTicket = "support.ticket.file";
    public const string ReplyTicket = "support.ticket.reply";
    public const string ResolveTicket = "support.ticket.resolve";
}

public record FileTicketCommand(
    string Subject,
    string Body,
    string? Priority = "normal",
    string? RelatedEntityId = null
);

public record ReplyTicketCommand(
    Guid TicketId,
    string Message
);
