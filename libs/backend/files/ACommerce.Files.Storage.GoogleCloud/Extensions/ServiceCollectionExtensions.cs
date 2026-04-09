using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Storage.GoogleCloud.Configuration;
using ACommerce.Files.Storage.GoogleCloud.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Files.Storage.GoogleCloud.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGoogleCloudStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GoogleCloudStorageOptions>()
            .Bind(configuration.GetSection(GoogleCloudStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IStorageProvider, GoogleCloudStorageProvider>();

        return services;
    }

    public static IServiceCollection AddGoogleCloudStorage(
        this IServiceCollection services,
        Action<GoogleCloudStorageOptions> configure)
    {
        services.AddOptions<GoogleCloudStorageOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IStorageProvider, GoogleCloudStorageProvider>();

        return services;
    }
}
