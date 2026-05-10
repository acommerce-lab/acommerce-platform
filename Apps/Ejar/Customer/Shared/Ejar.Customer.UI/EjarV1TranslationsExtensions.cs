using ACommerce.Compositions.Customer.L10n.Resx;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI;

/// <summary>
/// G4: تَسجيل طَبَقَة تَرجَمات Ejar V1 (overrides فَقَط) فَوق طَبَقَة القالَب
/// المُحايِدَة. يُستَدعى مِن Program.cs لِكلّ Frontend (Web/WASM/Maui) بَعد
/// <c>AddEjarCustomerUI()</c>.
///
/// <para><b>عَقد الطَبَقَة</b>: <c>Resources/Strings.resx</c> +
/// <c>Strings.ar.resx</c> في هذا الـ assembly تَحويان فَقَط مَفاتيح
/// التَخصيص (brand: app.name، voice: home.subtitle، home.cta.title).
/// أيّ مِفتاح آخَر يَنزَلِق لِلقالَب (Customer.Marketplace) تلقائيّاً —
/// لا حاجَة لِنَسخ كلّ المَفاتيح.</para>
///
/// <example>
/// <code>
/// // Apps/Ejar/Customer/Frontend/Ejar.WebAssembly/Program.cs:
/// builder.Services.AddEjarCustomerUI();         // adds template layer + L
/// builder.Services.AddEjarV1Translations();     // adds Ejar V1 layer (wins over template)
/// </code>
/// </example>
/// </summary>
public static class EjarV1TranslationsExtensions
{
    public static IServiceCollection AddEjarV1Translations(this IServiceCollection services)
    {
        // <Brand>Strings.resx + .ar.resx ⇒ overrides القالَب لِـ Ejar V1.
        // baseName يَجِب أن يُطابِق namespace + filename الذي يَستَخدِمه
        // ResourceManager (Ejar.Customer.UI = RootNamespace).
        services.AddTranslationLayer(
            assembly: typeof(EjarV1TranslationsExtensions).Assembly,
            baseName: "Ejar.Customer.UI.Resources.Strings");
        return services;
    }
}
