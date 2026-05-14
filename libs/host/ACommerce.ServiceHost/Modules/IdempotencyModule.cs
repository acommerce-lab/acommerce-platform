using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

public static class IdempotencyModule
{
    /// <summary>
    /// يُسَجِّل حِزمَة Idempotency: الـ <see cref="IdempotencyInterceptor"/>
    /// (singleton — يُحقَن في كُلّ engine) + الـ store (scoped — يَحقُن
    /// DbContext per-request).
    ///
    /// <code>
    /// host.UseIdempotency&lt;EjarOperationIdempotencyStore&gt;();
    /// </code>
    ///
    /// <para>قَبل هذا الـ module كانَ كُلّ تَطبيق يَكتُب:
    /// <code>
    /// services.AddSingleton&lt;IOperationInterceptor, IdempotencyInterceptor&gt;();
    /// services.AddScoped&lt;IOperationIdempotencyStore, EjarOperationIdempotencyStore&gt;();
    /// </code>
    /// الآن سَطر واحِد بِنَوع store.</para>
    /// </summary>
    public static ServiceHostBuilder UseIdempotency<TStore>(this ServiceHostBuilder host)
        where TStore : class, IOperationIdempotencyStore
    {
        var s = host.Builder.Services;
        s.AddSingleton<IOperationInterceptor, IdempotencyInterceptor>();
        s.AddScoped<IOperationIdempotencyStore, TStore>();
        return host;
    }
}
