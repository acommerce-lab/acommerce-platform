using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Subscriptions.Templates.Extensions;

public static class SubscriptionsTemplatesServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل <see cref="SubscriptionState"/> singleton — يستهلكه
    /// <see cref="AcSubscriptionGate"/> ويُحدّثه التطبيق بعد التحقّق
    /// من الاشتراك.
    /// </summary>
    public static IServiceCollection AddSubscriptionsTemplates(this IServiceCollection services)
    {
        services.AddSingleton<SubscriptionState>();
        return services;
    }
}
