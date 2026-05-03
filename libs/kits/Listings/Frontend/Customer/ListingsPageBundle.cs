using ACommerce.ClientHost.Pages;
using ACommerce.Kits.Listings.Frontend.Customer.Pages;

namespace ACommerce.Kits.Listings.Frontend.Customer;

/// <summary>
/// صفحات الإعلانات الافتراضيّة. التطبيق يُسجِّلها عبر
/// <c>AddKitPages(p =&gt; p.Add&lt;ListingsPageBundle&gt;())</c>،
/// ويستطيع <c>Rename</c>/<c>Hide</c> أيّ صفحة بحسب احتياجه.
/// </summary>
public sealed class ListingsPageBundle : IPageBundle
{
    public string BundleId => "listings";

    public IEnumerable<KitPage> Pages =>
    [
        new("listings.index",  typeof(AcListingExplorePage), "/listings"),
        new("listings.detail", typeof(AcListingDetailsPage), "/listings/{id}"),
        new("listings.mine",   typeof(AcMyListingsPage),     "/my-listings",     RequiresAuth: true),
        new("listings.create", typeof(AcCreateListingPage),  "/my-listings/new", RequiresAuth: true),
    ];
}
