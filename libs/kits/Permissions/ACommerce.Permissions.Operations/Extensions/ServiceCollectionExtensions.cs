using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Permissions.Operations.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل PermissionInterceptor. التطبيق يجب أن يُسجّل IPermissionResolver أولاً.
    /// </summary>
    public static IServiceCollection AddPermissionInterceptor(this IServiceCollection services)
    {
        services.AddScoped<PermissionInterceptor>();
        return services;
    }
}
