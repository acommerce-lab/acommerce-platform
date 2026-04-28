using ACommerce.OperationEngine.Interceptors;
using ACommerce.Subscriptions.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Subscriptions.Operations.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل مكتبة الاشتراكات. يتطلب من التطبيق توفير ISubscriptionProvider لربط
    /// الكيانات المادية للتطبيق بالواجهات.
    ///
    /// الاستخدام:
    ///   services.AddScoped&lt;ISubscriptionProvider, MyAppSubscriptionProvider&gt;();
    ///   services.AddSubscriptionInterceptors();
    ///
    /// ثم في Program.cs أو AddOperationInterceptors:
    ///   registry.Register(sp.GetRequiredService&lt;QuotaInterceptor&gt;());
    ///   registry.Register(sp.GetRequiredService&lt;QuotaConsumptionInterceptor&gt;());
    /// </summary>
    public static IServiceCollection AddSubscriptionInterceptors(this IServiceCollection services)
    {
        services.AddScoped<QuotaInterceptor>();
        services.AddScoped<QuotaConsumptionInterceptor>();
        return services;
    }

    /// <summary>
    /// نسخة كاملة: تسجل المعترضات في OperationInterceptorRegistry تلقائياً.
    /// تستخدم هذه إذا كنت تستخدم AddOperationInterceptors أيضاً.
    /// </summary>
    public static IServiceCollection AddSubscriptionInterceptors(
        this IServiceCollection services,
        bool autoRegister)
    {
        services.AddScoped<QuotaInterceptor>();
        services.AddScoped<QuotaConsumptionInterceptor>();

        if (autoRegister)
        {
            // تسجيل factory-based في الـ registry
            services.AddSingleton<IRegisterInterceptor>(sp => new InterceptorRegistration(
                "QuotaInterceptor",
                () => sp.CreateScope().ServiceProvider.GetRequiredService<QuotaInterceptor>()));
            services.AddSingleton<IRegisterInterceptor>(sp => new InterceptorRegistration(
                "QuotaConsumptionInterceptor",
                () => sp.CreateScope().ServiceProvider.GetRequiredService<QuotaConsumptionInterceptor>()));
        }

        return services;
    }
}

internal interface IRegisterInterceptor
{
    string Name { get; }
    IOperationInterceptor Resolve();
}

internal class InterceptorRegistration : IRegisterInterceptor
{
    public string Name { get; }
    private readonly Func<IOperationInterceptor> _factory;

    public InterceptorRegistration(string name, Func<IOperationInterceptor> factory)
    {
        Name = name;
        _factory = factory;
    }

    public IOperationInterceptor Resolve() => _factory();
}
