using ACommerce.OperationEngine.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Versions.Operations.Extensions;

public static class VersionsOperationsServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل المعترض الكوني للإصدارات. التطبيق يجب أن يحقن
    /// <see cref="IAppVersionGate"/> قبل (أو بعد) هذا الاستدعاء.
    ///
    /// <para>الاستخدام:
    /// <code>
    ///   services.AddScoped&lt;IAppVersionGate, MyAppVersionGate&gt;();
    ///   services.AddVersionGateInterceptor();
    /// </code>
    /// </para>
    ///
    /// <para>عند استخدام <c>AddOperationInterceptors</c>، يكفي إضافة المعترض
    /// المُسجَّل من DI داخل الـ registry callback:
    /// <code>
    ///   builder.Services.AddOperationInterceptors(registry =&gt;
    ///   {
    ///       registry.Register(sp.GetRequiredService&lt;VersionGateInterceptor&gt;());
    ///   });
    /// </code>
    /// أو الأبسط: استدعاء <see cref="VersionsBackend.VersionsKitExtensions.AddVersionsKit{TStore}"/>
    /// التي تتولّى التسجيل تلقائياً.</para>
    /// </summary>
    public static IServiceCollection AddVersionGateInterceptor(this IServiceCollection services)
    {
        // المعترض stateless: ينشأ مرّة كـ singleton، يحلّ التبعيّات من context.Services.
        services.AddSingleton<IOperationInterceptor, VersionGateInterceptor>();
        return services;
    }
}
