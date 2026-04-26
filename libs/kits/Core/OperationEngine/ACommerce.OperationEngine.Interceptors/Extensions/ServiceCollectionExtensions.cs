using ACommerce.OperationEngine.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.OperationEngine.Interceptors.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل OperationInterceptorRegistry كـ Singleton ويعطي callback لتسجيل المعترضات.
    /// يضم أيضاً أي IOperationInterceptor سبق تسجيله في DI (مثلاً عبر AddOperationJournal).
    ///
    /// الاستخدام:
    ///   services.AddOperationInterceptors(registry =>
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
            // يضم كل IOperationInterceptor مُسجَّل في DI (مثلاً JournalInterceptor)
            foreach (var interceptor in sp.GetServices<IOperationInterceptor>())
                registry.Register(interceptor);
            return registry;
        });

        // يجعل OpEngine يجده تلقائياً عبر DI
        services.AddSingleton<IInterceptorSource>(sp => sp.GetRequiredService<OperationInterceptorRegistry>());

        return services;
    }
}
