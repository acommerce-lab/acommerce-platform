// Legacy shim — استعمل ‍SupportOps المُكتَّب في ISupportStore.cs.
using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Support.Operations;

/// <summary>توافق خلفيّ — استخدم <see cref="SupportOps"/>.</summary>
public static class SupportOperations
{
    public static readonly OperationType FileTicket    = SupportOps.TicketOpen;
    public static readonly OperationType ReplyTicket   = SupportOps.TicketReply;
    public static readonly OperationType ResolveTicket = SupportOps.TicketStatusChange;
}
