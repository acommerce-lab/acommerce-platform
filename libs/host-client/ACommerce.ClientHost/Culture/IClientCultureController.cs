namespace ACommerce.ClientHost.Culture;

/// <summary>
/// واجِهة تَحَكُّم بِتَفضيلات الـ culture (لُغَة + thiem) عَبر قُيود
/// محاسبيّة. كلّ استِدعاء يَخرُج كَ OAM op يَدفَعه <see cref="OpEngine"/>
/// مُباشَرَةً (لا route، تَنفيذ مَحَلّيّ في ExecuteFunc) فتَستَفيد
/// post-interceptors المُسَجَّلة (telemetry، audit) بدون لَمس الصَفحَة
/// أَو الـ store.
///
/// <para>الصَفحات (Settings.razor، LanguageToggle widget) تَستَهلِك هذه
/// الواجِهة:
/// <code>
/// @inject IClientCultureController Culture
/// // ...
/// await Culture.SetLanguageAsync("en");
/// await Culture.SetThemeAsync("dark");
/// </code></para>
///
/// <para>التَنفيذ الافتراضيّ <see cref="DefaultClientCultureController"/>
/// يُحَدِّث <c>IUiPreferences</c> + يُطلِق <c>OnChanged</c> فيَنتَقِل
/// التَغيير عَبر <c>LocalStorageUiPersistence</c> + <c>L</c> + <c>ICultureContext</c>
/// (إن سَجَّل التَطبيق adapter يَقرأ مِن IUiPreferences) — كلّ ذلك مِن
/// عَمَليّة OAM واحِدَة.</para>
/// </summary>
public interface IClientCultureController
{
    /// <summary>يُطلِق <c>culture.set_language</c> op يُحَدِّث <c>IUiPreferences.Language</c>.</summary>
    Task SetLanguageAsync(string language, CancellationToken ct = default);

    /// <summary>يُطلِق <c>culture.set_theme</c> op يُحَدِّث <c>IUiPreferences.Theme</c>.</summary>
    Task SetThemeAsync(string theme, CancellationToken ct = default);

    /// <summary>يُطلِق <c>ui.set_city</c> op يُحَدِّث <c>IUiPreferences.City</c>.</summary>
    Task SetCityAsync(string city, CancellationToken ct = default);
}
