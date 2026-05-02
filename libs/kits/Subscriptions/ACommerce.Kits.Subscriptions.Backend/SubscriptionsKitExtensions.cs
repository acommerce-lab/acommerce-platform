using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Subscriptions.Backend;

public static class SubscriptionsKitExtensions
{
    /// <summary>
    /// يُسجِّل Subscriptions kit كاملاً: ٣ controllers (Subscriptions، Plans،
    /// Invoices) + ٣ stores يُنفّذها التطبيق.
    ///
    /// <para>الاستخدام في فترة الإطلاق التجريبيّ:</para>
    /// <code>
    /// builder.Services.AddSubscriptionsKit&lt;EjarSubscriptionStore, EjarPlanStore, EjarInvoiceStore&gt;(
    ///     opts => opts.OpenAccess = builder.Configuration.GetValue("Trial:OpenAccess", true));
    /// </code>
    ///
    /// <para>المتطلّبات: <c>OpEngine</c> مسجَّل (<c>AddOperationEngine</c>) قبل
    /// هذه الدالّة لأنّ <c>SubscriptionsController.Activate</c> يستدعيه.</para>
    /// </summary>
    public static IServiceCollection AddSubscriptionsKit<TSubStore, TPlanStore, TInvStore>(
        this IServiceCollection services,
        Action<SubscriptionsKitOptions>? configure = null)
        where TSubStore : class, ISubscriptionStore
        where TPlanStore : class, IPlanStore
        where TInvStore : class, IInvoiceStore
    {
        var opts = new SubscriptionsKitOptions();
        configure?.Invoke(opts);
        services.AddSingleton(opts);

        services.AddScoped<ISubscriptionStore, TSubStore>();
        services.AddScoped<IPlanStore,         TPlanStore>();
        services.AddScoped<IInvoiceStore,      TInvStore>();

        services.AddControllers()
            .AddApplicationPart(typeof(SubscriptionsController).Assembly);
        return services;
    }
}
