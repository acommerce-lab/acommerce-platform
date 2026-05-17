namespace ACommerce.Compositions.Customer.Marketplace.Home.Backend;

/// <summary>
/// مَنفَذ يُوَفِّر اقتِراحات بَحث (popular + recent). التَطبيق يُسَجِّل
/// impl يَعكِس سُوقه (يَمَني/سُعودي/إلخ). الافتِراضي قائِمَة فارِغَة.
/// </summary>
public interface IHomeSearchSuggestions
{
    /// <summary>كَلِمات/مُدُن شائِعَة. تُعرَض كَ chips في صَفحَة البَحث.</summary>
    IReadOnlyList<string> Popular { get; }

    /// <summary>اقتِراحات أَخيرَة لِلمُستَخدِم. الافتِراضي فارِغ — التَطبيقات
    /// الَّتي تَدعَم تَخزين تاريخ البَحث تُسَجِّل impl يَقرَأ مَن DB.</summary>
    IReadOnlyList<string> Recent { get; }
}

/// <summary>تَنفيذ فارِغ يَخدِم كَ fallback إذا التَطبيق لَم يُسَجِّل خاصَّاً.</summary>
public sealed class EmptyHomeSearchSuggestions : IHomeSearchSuggestions
{
    public IReadOnlyList<string> Popular { get; } = Array.Empty<string>();
    public IReadOnlyList<string> Recent  { get; } = Array.Empty<string>();
}
