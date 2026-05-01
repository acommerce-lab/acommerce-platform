using ACommerce.Kits.Reports.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Reports.Backend;

public static class ReportsKitExtensions
{
    public static IServiceCollection AddReportsKit<TStore>(
        this IServiceCollection services,
        Action<ReportsKitOptions>? configure = null)
        where TStore : class, IReportStore
    {
        var options = new ReportsKitOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddScoped<IReportStore, TStore>();
        services.AddControllers().AddApplicationPart(typeof(ReportsController).Assembly);
        return services;
    }
}
