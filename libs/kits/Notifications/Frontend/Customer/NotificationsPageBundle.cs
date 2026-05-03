using ACommerce.ClientHost.Pages;
using ACommerce.Kits.Notifications.Frontend.Customer.Pages;

namespace ACommerce.Kits.Notifications.Frontend.Customer;

public sealed class NotificationsPageBundle : IPageBundle
{
    public string BundleId => "notifications";

    public IEnumerable<KitPage> Pages =>
    [
        new("notifications.inbox", typeof(AcNotificationsInboxPage), "/notifications", RequiresAuth: true),
    ];
}
