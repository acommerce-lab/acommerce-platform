using ACommerce.ClientHost.Preferences;
using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using ACommerce.L10n.Blazor;

namespace Ashare.V3.Customer.UI.ClientHost;

/// <summary>
/// <see cref="ICultureContext"/> + <see cref="ILanguageContext"/> سُعودِيَّة لِـ V3.
/// تَتَجاوَز الـ <c>UiPreferencesCultureContext</c> اليَمَنِيَّة الَّتي يُسَجِّلها
/// قالَب Customer.Marketplace (TimeZone = Asia/Aden، Currency = YER).
///
/// <para>تُسَجَّل بَعد <see cref="AshareV3CustomerHostExtensions.AddAshareV3Customer"/>
/// لِيَفوز التَّسجيل الأَخير في DI ⇒ كُلّ widget يَستَهلِك ICultureContext
/// يَحصُل عَلى Asia/Riyadh + SAR.</para>
/// </summary>
internal sealed class AshareV3CultureContext : ICultureContext, ILanguageContext
{
    private readonly IUiPreferences _prefs;
    public AshareV3CultureContext(IUiPreferences prefs) => _prefs = prefs;

    public string Language       => string.IsNullOrEmpty(_prefs.Language) ? "ar" : _prefs.Language;
    public string TimeZoneId     => "Asia/Riyadh";
    public string NumeralSystem  => Language == "ar" ? "arabic-indic" : "latin";
    public TimeZoneInfo TimeZone => StaticCultureContext.ResolveTz(TimeZoneId);
    public string Currency       => "SAR";
    public bool   IsRtl          => Language == "ar";
}
