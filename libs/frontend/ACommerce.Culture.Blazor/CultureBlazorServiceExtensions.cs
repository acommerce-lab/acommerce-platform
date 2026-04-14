using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ACommerce.Culture.Blazor;

public static class CultureBlazorServiceExtensions
{
    /// <summary>
    /// Registers the culture stack for a Blazor Server app.  Scoped per
    /// circuit so each connected user has their own timezone/language.
    /// </summary>
    public static IServiceCollection AddBlazorCultureStack(this IServiceCollection s)
    {
        s.TryAddSingleton<INumeralNormalizer, DefaultNumeralNormalizer>();
        s.TryAddSingleton<IDateTimeNormalizer, DefaultDateTimeNormalizer>();
        s.TryAddSingleton<IPhoneNumberValidator, RegexPhoneNumberValidator>();
        s.TryAddScoped<MutableCultureContext>();
        s.TryAddScoped<ICultureContext>(sp => sp.GetRequiredService<MutableCultureContext>());
        s.TryAddScoped<BrowserCultureProbe>();
        s.TryAddScoped<CultureTimeFormatter>();
        return s;
    }
}
