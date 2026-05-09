using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ClientHost.KitApi;

/// <summary>
/// تَسجيل DI للـ KitApi pipeline. يُستدعى من التَطبيق مرّة واحدة:
/// <code>
/// services.AddKitApiPipeline(http: sp => sp.GetRequiredService&lt;EjarCircuitHttp&gt;().Client)
///         .AddAnalyzer&lt;RequiredAuthAnalyzer&gt;()
///         .AddInterceptor&lt;TelemetryInterceptor&gt;()
///         .AddInterceptor&lt;RetryOn401Interceptor&gt;();
/// </code>
/// كلّ <c>HttpXxxApiClient</c> في كلّ الكيتس يَحقن <see cref="KitHttpClient"/>
/// ويُستفيد من الـ pipeline بدون أيّ إعداد إضافيّ.
/// </summary>
public static class KitApiServiceCollectionExtensions
{
    public static KitApiBuilder AddKitApiPipeline(
        this IServiceCollection services,
        Func<IServiceProvider, HttpClient> httpResolver)
    {
        services.AddScoped(sp =>
            new KitHttpClient(
                httpResolver(sp),
                sp.GetServices<IKitApiAnalyzer>(),
                sp.GetServices<IKitApiInterceptor>()));
        return new KitApiBuilder(services);
    }
}

public sealed class KitApiBuilder
{
    public IServiceCollection Services { get; }
    public KitApiBuilder(IServiceCollection services) => Services = services;

    public KitApiBuilder AddAnalyzer<TAnalyzer>() where TAnalyzer : class, IKitApiAnalyzer
    {
        Services.AddScoped<IKitApiAnalyzer, TAnalyzer>();
        return this;
    }

    public KitApiBuilder AddInterceptor<TInterceptor>() where TInterceptor : class, IKitApiInterceptor
    {
        Services.AddScoped<IKitApiInterceptor, TInterceptor>();
        return this;
    }
}
