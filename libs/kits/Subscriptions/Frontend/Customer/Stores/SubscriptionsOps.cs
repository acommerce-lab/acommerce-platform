using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;

public static class SubscriptionsOps
{
    public static Operation ListPlans() => Entry
        .Create("subscriptions.plans.list")
        .From("User:current",        1, ("role", "browser"))
        .To("Server:subscriptions",  1, ("role", "catalog"))
        .Build();

    public static Operation GetActive() => Entry
        .Create("subscription.get_active")
        .From("User:current",        1, ("role", "subscriber"))
        .To("Server:subscriptions",  1, ("role", "source"))
        .Build();

    public static Operation Activate(string planId) => Entry
        .Create("subscription.activate")
        .From("User:current",        1, ("role", "subscriber"))
        .To($"Plan:{planId}",        1, ("role", "subject"))
        .Tag("planId", planId)
        .Build();
}
