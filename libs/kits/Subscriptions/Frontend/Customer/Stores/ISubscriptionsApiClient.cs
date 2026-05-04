using ACommerce.Subscriptions.Operations.Abstractions;

namespace ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;

/// <summary>
/// عميل HTTP خاصّ بـ Subscriptions kit. يَعرف <c>PlansController</c> +
/// <c>SubscriptionsController</c> wire shapes:
/// <list type="bullet">
///   <item><c>GET /plans</c> ⇒ <c>PlanDto[]</c></item>
///   <item><c>GET /me/subscription</c> ⇒ <c>SubscriptionDto?</c></item>
///   <item><c>POST /subscriptions/activate</c> ⇒ <c>SubscriptionDto</c></item>
/// </list>
/// </summary>
public interface ISubscriptionsApiClient
{
    Task<IReadOnlyList<IPlan>> ListPlansAsync(CancellationToken ct = default);
    Task<ISubscription?>       GetActiveAsync(CancellationToken ct = default);
    Task<ISubscription?>       ActivateAsync(string planId, CancellationToken ct = default);
}
