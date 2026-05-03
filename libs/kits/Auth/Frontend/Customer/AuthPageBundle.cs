using ACommerce.ClientHost.Pages;
using ACommerce.Kits.Auth.Frontend.Customer.Pages;

namespace ACommerce.Kits.Auth.Frontend.Customer;

public sealed class AuthPageBundle : IPageBundle
{
    public string BundleId => "auth";

    public IEnumerable<KitPage> Pages =>
    [
        new("auth.login", typeof(AcLoginPage), "/login"),
    ];
}
