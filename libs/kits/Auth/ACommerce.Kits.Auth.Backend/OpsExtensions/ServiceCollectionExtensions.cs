using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Auth.Operations.Extensions;

public static class AuthOperationsServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل <see cref="AuthGateInterceptor"/> الكونيّ. التطبيق يستدعي هذه الدالّة
    /// مرّة في <c>Program.cs</c>؛ المعترض يلتقطه <c>AddOperationInterceptors</c>
    /// تلقائياً عبر DI.
    ///
    /// <para>المعترض stateless — يحلّ تبعيّاته (<see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/>)
    /// من <c>OperationContext.Services</c> بدل التقاط مرجع scoped في singleton.</para>
    /// </summary>
    public static IServiceCollection AddAuthGateInterceptor(this IServiceCollection services)
    {
        services.AddSingleton<IOperationInterceptor, AuthGateInterceptor>();
        return services;
    }
}
