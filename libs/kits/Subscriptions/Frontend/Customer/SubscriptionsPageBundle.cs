using ACommerce.ClientHost.Pages;
using ACommerce.Kits.Subscriptions.Frontend.Customer.Pages;

namespace ACommerce.Kits.Subscriptions.Frontend.Customer;

public sealed class SubscriptionsPageBundle : IPageBundle
{
    public string BundleId => "subscriptions";

    public IEnumerable<KitPage> Pages =>
    [
        new("subscriptions.plans", typeof(AcPlansPage), "/plans"),
    ];
}
