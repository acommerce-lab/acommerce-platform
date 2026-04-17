using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ACommerce.Culture.Interceptors;

public static class CultureServiceCollectionExtensions
{
    /// <summary>
    /// Registers:
    ///   ICultureContext       (Scoped, MutableCultureContext)
    ///   INumeralNormalizer    (Singleton, DefaultNumeralNormalizer)
    ///   IDateTimeNormalizer   (Singleton, DefaultDateTimeNormalizer)
    ///   IPhoneNumberValidator (Singleton, RegexPhoneNumberValidator)
    ///   NumeralToLatinSaveInterceptor (Scoped)
    ///   DateTimeUtcSaveInterceptor    (Scoped)
    /// </summary>
    public static IServiceCollection AddCultureStack(this IServiceCollection services)
    {
        services.TryAddSingleton<INumeralNormalizer, DefaultNumeralNormalizer>();
        services.TryAddSingleton<IDateTimeNormalizer, DefaultDateTimeNormalizer>();
        services.TryAddSingleton<IPhoneNumberValidator, RegexPhoneNumberValidator>();
        services.TryAddScoped<MutableCultureContext>();
        services.TryAddScoped<ICultureContext>(sp => sp.GetRequiredService<MutableCultureContext>());
        services.AddScoped<NumeralToLatinSaveInterceptor>();
        services.AddScoped<DateTimeUtcSaveInterceptor>();
        return services;
    }

    /// <summary>Plugs CultureContextMiddleware into the pipeline (call after UseRouting).</summary>
    public static IApplicationBuilder UseCultureContext(this IApplicationBuilder app)
        => app.UseMiddleware<CultureContextMiddleware>();
}
