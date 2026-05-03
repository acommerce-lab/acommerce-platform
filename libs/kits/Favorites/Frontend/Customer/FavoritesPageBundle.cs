using ACommerce.ClientHost.Pages;
using ACommerce.Kits.Favorites.Frontend.Customer.Pages;

namespace ACommerce.Kits.Favorites.Frontend.Customer;

public sealed class FavoritesPageBundle : IPageBundle
{
    public string BundleId => "favorites";

    public IEnumerable<KitPage> Pages =>
    [
        new("favorites.list", typeof(AcFavoritesPage), "/favorites", RequiresAuth: true),
    ];
}
