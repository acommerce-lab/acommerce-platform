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
    /// يُسَجِّل كلّ خَدَمات القالَب. <strong>المُتَطَلَّب</strong>: أن يَكون
    /// <c>HttpClient</c> مُسَجَّلاً باسم <c>"ejar"</c> + <c>AppVersionInfo</c>
    /// singleton قَبل الاستدعاء (تماماً كَما كانَ V1 OLD).
    /// </summary>
    public static IServiceCollection AddEjarCustomer(this IServiceCollection services)
        => services.AddEjarCustomerUI();
}
