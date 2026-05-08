using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Favorites.Frontend.Customer.Stores;

public static class FavoritesOps
{
    public static Operation List() => Entry
        .Create("favorites.list")
        .From("User:current",      1, ("role", "owner"))
        .To("Server:favorites",    1, ("role", "source"))
        .Build();

    public static Operation Toggle(string listingId) => Entry
        .Create("favorite.toggle")
        .From("User:current",      1, ("role", "actor"))
        .To($"Listing:{listingId}",1, ("role", "subject"))
        .Tag("id", listingId)
        .Tag("realtime_broadcast", "true")
        .Build();
}
