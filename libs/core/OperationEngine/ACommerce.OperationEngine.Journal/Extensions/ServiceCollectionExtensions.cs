using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.SharedKernel.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ACommerce.OperationEngine.Journal.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يُفعِّل حفظ كل عملية في جدول journal_entries.
    ///
    /// يتطلب:
    ///   - استدعاء AddOperationInterceptors (أو يُسجَّل registry محلي إذا لم يُستدعَ)
    ///   - IRepositoryFactory مُسجَّلة في DI (يوفرها AddACommerceSQLite أو غيره)
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

        // سجّل IAccountQuery لاستعلامات الحسابات
        services.TryAddScoped<IAccountQuery, JournalAccountQuery>();

        return services;
    }
}
