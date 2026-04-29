using ACommerce.Kits.Versions.Operations;
using ACommerce.Kits.Versions.Operations.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Versions.Backend;

public static class VersionsKitExtensions
{
    /// <summary>
    /// يسجّل Versions Kit كاملاً:
    /// <list type="bullet">
    ///   <item><see cref="IVersionStore"/> ↦ <typeparamref name="TStore"/></item>
    ///   <item><see cref="IAppVersionGate"/> ↦ <see cref="StoreBackedAppVersionGate"/> (تطبيق افتراضيّ)</item>
    ///   <item>المعترض الكوني <see cref="VersionGateInterceptor"/></item>
    ///   <item>المتحكمات <see cref="VersionsController"/> + <see cref="AdminVersionsController"/></item>
    /// </list>
    /// إن أراد التطبيق تطبيقاً مخصّصاً للـ gate (مع cache مثلاً) فليسجّله قبل
    /// استدعاء هذا الـ extension — التسجيل المسبق يفوز.
    /// </summary>
    public static IServiceCollection AddVersionsKit<TStore>(
        this IServiceCollection services,
        VersionGateOptions? options = null)
        where TStore : class, IVersionStore
    {
        services.AddScoped<IVersionStore, TStore>();
        services.AddSingleton(options ?? new VersionGateOptions());
        services.TryAddDefaultGate();
        services.AddHttpContextAccessor();
        services.AddVersionGateInterceptor();
        services.AddControllers().AddApplicationPart(typeof(VersionsController).Assembly);
        return services;
    }

    /// <summary>
    /// النسخة المتقدّمة: التطبيق يحقن <c>IAppVersionGate</c> مخصّصاً (مثلاً يقرأ من
    /// remote config) — الـ Kit لا يضيف <see cref="StoreBackedAppVersionGate"/> ولا
    /// يُلزم التطبيق بـ <see cref="IVersionStore"/> (يستطيع الإدارة من نظام آخر).
    /// </summary>
    public static IServiceCollection AddVersionsKitWithCustomGate(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddVersionGateInterceptor();
        services.AddControllers().AddApplicationPart(typeof(VersionsController).Assembly);
        return services;
    }

    private static IServiceCollection TryAddDefaultGate(this IServiceCollection services)
    {
        // إذا لم يُسجَّل IAppVersionGate من قبل التطبيق، نضيف الافتراضيّ.
        if (!services.Any(d => d.ServiceType == typeof(IAppVersionGate)))
            services.AddScoped<IAppVersionGate, StoreBackedAppVersionGate>();
        return services;
    }
}
