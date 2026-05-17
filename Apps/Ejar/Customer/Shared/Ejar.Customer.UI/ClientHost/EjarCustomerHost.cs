using ACommerce.ClientHost.Auth;
using ACommerce.Kits.DynamicAttributes.Frontend.Customer.Stores;
using ACommerce.Kits.Taxonomy.Frontend.Customer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI.ClientHost;

/// <summary>
/// V1 thin host — يُفَوِّض لِكامِل خَدَمات قالَب Customer.Marketplace
/// المَنقولة من V1 الأَصلي. هَكذا يَحفَظ V1 تَصميمه وأَداءه القَديم
/// (نَفس Pages, Services, Bindings, Interceptors, Interpreters) ولكن
/// كلّها تَعيش في طَبَقَة القالَب — V1 (Ejar.Customer.UI) لا يَحوي شَيئاً
/// خاصّاً به الآن. التَّخصيص الوَحيد لِلتَطبيق: branding.css في host.
/// </summary>
public static class EjarCustomerHostExtensions
{
    /// <summary>
    /// يُسَجِّل كلّ خَدَمات القالَب + طَبَقَة تَرجَمات Ejar V1 (overrides).
    /// <strong>المُتَطَلَّب</strong>: أن يَكون <c>HttpClient</c> مُسَجَّلاً باسم
    /// <c>"ejar"</c> + <c>AppVersionInfo</c> singleton قَبل الاستدعاء.
    ///
    /// <para><b>تَرتيب الطَبَقات (G4)</b>: AddEjarCustomerUI يُسَجِّل
    /// طَبَقَة القالَب (الأَدنى) + AddLayeredTranslation. ثُمّ
    /// AddEjarV1Translations يُضيف طَبَقَة Ejar V1 فَوقها (الأَعلى) ⇒
    /// مَفاتيح Ejar تَفوز، الباقي يَنزَلِق لِلقالَب.</para>
    /// </summary>
    public static IServiceCollection AddEjarCustomer(this IServiceCollection services)
    {
        services.AddEjarCustomerUI();
        services.AddEjarV1Translations();

        // DynamicAttributes frontend kit — HttpAttributesStore يَستَهلِك
        // HttpClient "ejar" لِجَلب القَوالِب مَن /dynamic-attributes/templates/{scope}.
        // <b>مُهِمّ</b>: نَحقُن <c>AuthenticatedHttpClient.Client</c> صَراحَةً
        // (لَه BaseAddress + Bearer header) — الـ default HttpClient فارِغ
        // وَلَو حُقِن سَيُرسِل لِـ <c>/dynamic-attributes/...</c> بِلا host.
        services.AddScoped<IAttributesStore>(sp =>
            new HttpAttributesStore(sp.GetRequiredService<AuthenticatedHttpClient>().Client));

        // Taxonomy frontend kit — HttpTaxonomyStore يَجلِب شَجَرَة الفِئات
        // مَن /taxonomy/{rootCode} ويُخَزِّنها في الذاكِرَة. AcTaxonomyTreeSelect
        // في wizard CreateListing + قِسم Home/Explore يَستَهلِكه.
        services.AddScoped<ITaxonomyStore>(sp =>
            new HttpTaxonomyStore(sp.GetRequiredService<AuthenticatedHttpClient>().Client));
        return services;
    }
}
