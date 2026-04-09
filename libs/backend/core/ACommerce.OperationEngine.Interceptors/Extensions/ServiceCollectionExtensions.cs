using ACommerce.OperationEngine.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.OperationEngine.Interceptors.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل OperationInterceptorRegistry كـ Singleton ويعطي callback لتسجيل المعترضات.
    ///
    /// الاستخدام:
    ///   services.AddOperationInterceptors(registry =&gt;
    ///   {
    ///       registry.Register(new TaggedInterceptor(...));
    ///       registry.Register(new SubscriptionQuotaInterceptor(...));
    ///   });
    /// </summary>
    public static IServiceCollection AddOperationInterceptors(
        this IServiceCollection services,
        Action<OperationInterceptorRegistry>? configure = null)
    {
        services.AddSingleton<OperationInterceptorRegistry>(sp =>
        {
            var registry = new OperationInterceptorRegistry();
            configure?.Invoke(registry);
            return registry;
        });

        // يجعل OpEngine يجده تلقائياً عبر DI
        services.AddSingleton<IInterceptorSource>(sp => sp.GetRequiredService<OperationInterceptorRegistry>());

        return services;
    }
}
