using ACommerce.Client.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Client.Http.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل HttpDispatcher كـ IOperationDispatcher + يربط HttpClient.
    /// </summary>
    public static IServiceCollection AddAshareHttpDispatcher(
        this IServiceCollection services,
        Action<HttpDispatcherOptions>? configure = null,
        Action<HttpRouteRegistry>? routes = null)
    {
        var options = new HttpDispatcherOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        var registry = new HttpRouteRegistry();
        routes?.Invoke(registry);
        services.AddSingleton(registry);

        services.AddSingleton<HttpDispatcher>();
        services.AddSingleton<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

        return services;
    }
}
