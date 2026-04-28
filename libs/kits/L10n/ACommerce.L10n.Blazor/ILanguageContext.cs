namespace ACommerce.L10n.Blazor;

/// <summary>
/// جسر قراءة اللغة الحالية من حالة التطبيق.
/// كل تطبيق يُنفّذه بطريقته (عادةً قراءة من AppStore.Ui.Language).
/// </summary>
public interface ILanguageContext
{
    /// <summary>"ar" | "en" (أو أي لغة أخرى مدعومة مستقبلاً).</summary>
    string Language { get; }

    /// <summary>هل الاتجاه يمين-إلى-يسار؟ (true للعربية).</summary>
    bool IsRtl { get; }
}
