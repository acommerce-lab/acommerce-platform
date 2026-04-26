using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Files.Operations.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل FileService الذي يغلّف IStorageProvider بقيود محاسبية.
    /// يتطلب وجود IStorageProvider مُسجّل مسبقاً (Aliyun, GCS, Local...).
    /// </summary>
    public static IServiceCollection AddFileOperations(this IServiceCollection services)
    {
        services.AddScoped<FileService>();
        return services;
    }
}
