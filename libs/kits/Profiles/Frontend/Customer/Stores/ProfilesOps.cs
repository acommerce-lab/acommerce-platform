using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Profiles.Frontend.Customer.Stores;

public static class ProfilesOps
{
    public static Operation GetMine() => Entry
        .Create("profile.get_mine")
        .From("User:current",   1, ("role", "self"))
        .To("Server:profiles",  1, ("role", "source"))
        .Build();

    public static Operation Update() => Entry
        .Create("profile.update")
        .From("User:current",   1, ("role", "owner"))
        .To("Server:profiles",  1, ("role", "subject"))
        .Build();
}
