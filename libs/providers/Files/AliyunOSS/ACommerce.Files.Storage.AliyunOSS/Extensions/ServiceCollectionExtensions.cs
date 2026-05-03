using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Storage.AliyunOSS.Configuration;
using ACommerce.Files.Storage.AliyunOSS.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ACommerce.Files.Storage.AliyunOSS.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAliyunOSSFileStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AliyunOSSOptions>()
            .Bind(configuration.GetSection(AliyunOSSOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IStorageProvider, AliyunOSSStorageProvider>();

        return services;
    }

    public static IServiceCollection AddAliyunOSSFileStorage(
        this IServiceCollection services,
        Action<AliyunOSSOptions> configure)
    {
        services.AddOptions<AliyunOSSOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IStorageProvider, AliyunOSSStorageProvider>();

        return services;
    }
}
