namespace ACommerce.L10n.Blazor;

/// <summary>
/// ProviderContract: مَصدَر التَرجَمات.
///
/// <para>التَنفيذات: <c>EmbeddedTranslationProvider</c> (in-code dicts)،
/// <c>ResxTranslationProvider</c> (.resx + ResourceManager)،
/// <c>LayeredTranslationProvider</c> (chain of providers بِأَولَويّة).</para>
///
/// <para><b>عَقد البَحث</b>: <see cref="TryTranslate"/> يَرُدّ <c>null</c>
/// لو المِفتاح غير مَوجود في هذه الطَبَقَة بِالتَحديد. <see cref="Translate"/>
/// يَرُدّ المِفتاح نَفسه كَ fallback إذا فَشَل البَحث (تَوافُق رَجعيّ).</para>
/// </summary>
public interface ITranslationProvider
{
    /// <summary>
    /// يُحاوِل جَلب التَرجَمَة. <c>null</c> = "غير مَوجود في هذه الطَبَقَة"
    /// (لِيَستَطيع <see cref="LayeredTranslationProvider"/> تَجاوُز الطَبَقَة
    /// والاستِمرار في السَلسَلَة). تَنفيذات قَديمَة تَستَطيع الاحتِفاظ بِـ
    /// <see cref="Translate"/> مُباشَرَةً عَبر default impl.
    /// </summary>
    string? TryTranslate(string key, string language);

    /// <summary>
    /// API الـ user-facing: نَصّ مَضمون. لَو لَم تَجِد طَبَقَة المِفتاح،
    /// يُعاد المِفتاح نَفسه (debugging signal — يَظهَر في الواجِهَة).
    /// تَنفيذات تُنادي <see cref="TryTranslate"/> داخِليّاً (default impl
    /// أَدناه).
    /// </summary>
    string Translate(string key, string language) => TryTranslate(key, language) ?? key;
}
