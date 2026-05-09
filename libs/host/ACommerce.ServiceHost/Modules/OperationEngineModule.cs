using ACommerce.Kits.Auth.Operations.Extensions;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Extensions;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using ACommerce.Subscriptions.Operations.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ACommerce.ServiceHost;

public static class OperationEngineModule
{
    /// <summary>
    /// يُسجِّل البنية التحتيّة الكاملة لـ OperationEngine في خطوة واحدة:
    /// <list type="bullet">
    ///   <item><c>OpEngine</c> + Interceptor registry.</item>
    ///   <item>Pre-gates: AuthGate (الـ <c>auth_required</c>) + SubscriptionGate
    ///         (الـ <c>requires_subscription</c>).</item>
    ///   <item><c>CrudActionInterceptor</c> (مسار generic CRUD عبر MediatR).</item>
    ///   <item>MediatR (يكتشف handlers في الـ assembly المُمرَّر،
    ///         افتراضيّاً Entry assembly للتطبيق).</item>
    /// </list>
    /// التطبيق يستطيع تسجيل interceptors إضافيّة بعد هذه الدالّة عبر
    /// <c>builder.Services.AddSingleton&lt;IOperationInterceptor, MyInterceptor&gt;()</c>
    /// — كلّ <c>IOperationInterceptor</c> singleton يُلتقَط تلقائيّاً.
    /// </summary>
    public static ServiceHostBuilder UseOperationEngine(
        this ServiceHostBuilder host,
        Assembly? mediatorAssembly = null)
    {
        var s = host.Builder.Services;

        s.AddOperationEngine();
        s.AddOperationInterceptors(registry =>
        {
            registry.Register(new CrudActionInterceptor());
        });
        s.AddAuthGateInterceptor();
        s.AddSubscriptionGateInterceptor();

        s.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(mediatorAssembly ?? Assembly.GetEntryAssembly()!);
        });

        return host;
    }
}
