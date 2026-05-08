using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Notifications.Frontend.Customer.Stores;

public static class NotificationsOps
{
    public static Operation List() => Entry
        .Create("notifications.list")
        .From("User:current",       1, ("role", "recipient"))
        .To("Server:notifications", 1, ("role", "source"))
        .Build();

    public static Operation MarkRead(string id) => Entry
        .Create("notification.mark_read")
        .From("User:current",       1, ("role", "reader"))
        .To($"Notification:{id}",   1, ("role", "subject"))
        .Tag("id", id)
        .Build();

    public static Operation MarkAllRead() => Entry
        .Create("notifications.mark_all_read")
        .From("User:current",       1, ("role", "reader"))
        .To("Server:notifications", 1, ("role", "source"))
        .Build();
}
