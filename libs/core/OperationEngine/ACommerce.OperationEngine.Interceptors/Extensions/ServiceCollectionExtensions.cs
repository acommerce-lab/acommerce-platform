using ACommerce.OperationEngine.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.OperationEngine.Interceptors.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يُسَجِّل <see cref="OperationInterceptorRegistry"/> كـ <b>Scoped</b> ويُعطي
    /// callback لِتَسجيل المُعتَرضات الثابِتَة (DI-independent). ضَمّ أيضاً
    /// أيّ <see cref="IOperationInterceptor"/> مُسَجَّل في DI (Scoped/Singleton).
    ///
    /// <para><b>Scoped</b> بَدَل Singleton: يُلائم تَطبيقات يَحوي بَعض
    /// IOperationInterceptors فيها dependencies scoped (مَثَل
    /// CultureLocalizationInterceptor الذي يَعتَمِد عَلى ICultureContext
    /// scoped). تَسجيل registry كَ Singleton كانَ يَنتُج
    /// <c>DirectScopedResolvedFromRootException</c> في WASM dev (validate
    /// scopes مُفَعَّل) ⇒ كان يَكسِر الإقلاع.</para>
    ///
    /// <para>الـ <c>configure</c> callback يُستَدعى مَرّة لكلّ scope. التَكلِفَة
    /// زَهيدَة (إنشاء List + Register) ولا توجَد side effects.</para>
    ///
    /// الاستخدام:
    /// <code>
    /// services.AddOperationInterceptors(registry =>
    /// {
    ///     registry.Register(new TaggedInterceptor(...));
    ///     registry.Register(new SubscriptionQuotaInterceptor(...));
    /// });
    /// </code>
    /// </summary>
    public static IServiceCollection AddOperationInterceptors(
        this IServiceCollection services,
        Action<OperationInterceptorRegistry>? configure = null)
    {
        services.AddScoped<OperationInterceptorRegistry>(sp =>
        {
            var registry = new OperationInterceptorRegistry();
            configure?.Invoke(registry);
            // يضم كل IOperationInterceptor مُسجَّل في DI (مثلاً JournalInterceptor،
            // CultureLocalizationInterceptor، …) — Scope الحاليّ يَستَنبِط
            // scoped instances بِلا مَشاكِل.
            foreach (var interceptor in sp.GetServices<IOperationInterceptor>())
                registry.Register(interceptor);
            return registry;
        });

        // يجعل OpEngine يجده تلقائياً عبر DI
        services.AddScoped<IInterceptorSource>(sp => sp.GetRequiredService<OperationInterceptorRegistry>());

        return services;
    }
}
