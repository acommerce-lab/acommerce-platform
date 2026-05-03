using ACommerce.OperationEngine.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.OperationEngine.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOperationEngine(this IServiceCollection services)
    {
        services.AddScoped<OpEngine>();
        return services;
    }
}
