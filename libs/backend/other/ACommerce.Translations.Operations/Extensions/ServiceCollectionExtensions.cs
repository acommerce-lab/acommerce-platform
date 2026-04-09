using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.Translations.Operations.Entities;
using ACommerce.Translations.Operations.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Translations.Operations.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل TranslationService ويُسجّل كيانات Translation/Language في الـ EntityDiscoveryRegistry.
    /// </summary>
    public static IServiceCollection AddTranslationOperations(this IServiceCollection services)
    {
        EntityDiscoveryRegistry.RegisterEntity(typeof(Translation));
        EntityDiscoveryRegistry.RegisterEntity(typeof(Language));

        services.AddScoped<TranslationService>();
        return services;
    }

    /// <summary>
    /// يسجّل TranslationInterceptor (PostInterceptor) الذي يقرأ Accept-Language
    /// ويستبدل الحقول المترجَمة على الكيان في ctx.Items قبل أن يصل للـ wire.
    /// يتطلب AddHttpContextAccessor().
    /// </summary>
    public static IServiceCollection AddTranslationInterceptor(this IServiceCollection services, string fallbackLanguage = "ar")
    {
        services.AddHttpContextAccessor();
        services.AddScoped<TranslationInterceptor>(sp => new TranslationInterceptor(
            sp.GetRequiredService<TranslationService>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TranslationInterceptor>>(),
            fallbackLanguage));
        return services;
    }
}
