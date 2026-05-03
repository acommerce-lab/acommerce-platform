using ACommerce.ClientHost.Pages;
using ACommerce.Kits.Profiles.Frontend.Customer.Pages;

namespace ACommerce.Kits.Profiles.Frontend.Customer;

public sealed class ProfilesPageBundle : IPageBundle
{
    public string BundleId => "profiles";

    public IEnumerable<KitPage> Pages =>
    [
        new("profiles.me", typeof(AcProfilePage), "/me", RequiresAuth: true),
    ];
}
