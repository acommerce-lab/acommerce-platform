using ACommerce.OperationEngine.Accounts;
using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    /// <summary>
    /// يُفعِّل حفظ كل عملية في جدول journal_entries.
    ///
    /// يتطلب:
    ///   - تسجيل JournalEntry مع EntityDiscoveryRegistry (يتم تلقائياً هنا)
    ///   - استدعاء AddOperationInterceptors (أو يُسجَّل registry محلي إذا لم يُستدعَ)
    ///
    /// الاستخدام:
    ///   services.AddOperationJournal();
    /// </summary>
    public static IServiceCollection AddOperationJournal(this IServiceCollection services)
    {
        EntityDiscoveryRegistry.RegisterEntity<JournalEntry>();

        // سجّل المعترض كـ singleton ليُلتقَط من AddOperationInterceptors
        services.AddSingleton<IOperationInterceptor, JournalInterceptor>();

        // إذا لم يُستدعَ AddOperationInterceptors بعد، تأكد من وجود registry ومصدر للمعترضات
        services.TryAddSingleton<OperationInterceptorRegistry>(sp =>
        {
            var registry = new OperationInterceptorRegistry();
            foreach (var interceptor in sp.GetServices<IOperationInterceptor>())
                registry.Register(interceptor);
            return registry;
        });
        services.TryAddSingleton<IInterceptorSource>(sp => sp.GetRequiredService<OperationInterceptorRegistry>());

        // سجّل IAccountQuery لاستعلامات الحسابات (Phase 0.3)
        services.TryAddScoped<IAccountQuery, JournalAccountQuery>();

        return services;
    }
}
