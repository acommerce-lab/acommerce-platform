using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Support.Frontend.Customer.Stores;

public static class SupportOps
{
    public static Operation ListTickets() => Entry
        .Create("support.tickets.list")
        .From("User:current",   1, ("role", "owner"))
        .To("Server:support",   1, ("role", "source"))
        .Build();

    public static Operation CreateTicket(string subject) => Entry
        .Create("support.ticket.create")
        .From("User:current",   1, ("role", "complainant"))
        .To("Server:support",   1, ("role", "issuer"))
        .Tag("subject", subject)
        .Build();

    public static Operation Reply(string ticketId) => Entry
        .Create("support.ticket.reply")
        .From("User:current",        1, ("role", "responder"))
        .To($"Ticket:{ticketId}",    1, ("role", "subject"))
        .Tag("id", ticketId)
        .Build();
}
