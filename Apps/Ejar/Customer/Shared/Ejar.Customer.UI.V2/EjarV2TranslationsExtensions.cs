using ACommerce.Compositions.Customer.L10n.Resx;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI.V2;

/// <summary>
/// G4: تَسجيل طَبَقَة تَرجَمات Ejar V2. حاليّاً تَحوي مِفتاحاً واحِداً
/// (<c>app.name</c>) لِأنّ Customer.Ledger لا يَستَخدِم L[] في صَفحاته
/// بَعد. الإطار جاهِز لِلإضافات لاحِقاً.
/// </summary>
public static class EjarV2TranslationsExtensions
{
    public static IServiceCollection AddEjarV2Translations(this IServiceCollection services)
    {
        services.AddTranslationLayer(
            assembly: typeof(EjarV2TranslationsExtensions).Assembly,
            baseName: "Ejar.Customer.UI.V2.Resources.Strings");
        return services;
    }
}
