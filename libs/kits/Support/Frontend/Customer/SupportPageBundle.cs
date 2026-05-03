using ACommerce.ClientHost.Pages;
using ACommerce.Kits.Support.Frontend.Customer.Pages;

namespace ACommerce.Kits.Support.Frontend.Customer;

public sealed class SupportPageBundle : IPageBundle
{
    public string BundleId => "support";

    public IEnumerable<KitPage> Pages =>
    [
        new("support.tickets", typeof(AcTicketsPage), "/support", RequiresAuth: true),
    ];
}
