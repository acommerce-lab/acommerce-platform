using ACommerce.Compositions.Customer.L10n.Resx;
using Microsoft.Extensions.DependencyInjection;

namespace Ashare.V3.Customer.UI;

/// <summary>
/// طَبَقَة تَرجَمَة Ashare V3 — تُسَجَّل بَعد طَبَقَة Ejar V1
/// (في <c>AshareV3CustomerHostExtensions.AddAshareV3Customer</c>) فَتَفوز
/// عَلى مَفاتيحها (<c>app.name</c>، <c>home.*</c>). أَيّ مِفتاح غَير
/// مُتَجاوَز يَنزَلِق لِـ Ejar V1 → قالَب Customer.Marketplace.
/// </summary>
public static class AshareV3TranslationsExtensions
{
    public static IServiceCollection AddAshareV3Translations(this IServiceCollection services)
    {
        services.AddTranslationLayer(
            assembly: typeof(AshareV3TranslationsExtensions).Assembly,
            baseName: "Ashare.V3.Customer.UI.Resources.Strings");
        return services;
    }
}
