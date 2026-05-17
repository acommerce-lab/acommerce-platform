namespace ACommerce.Compositions.Customer.Marketplace.Home.Backend;

/// <summary>سَطر صَفحَة قانونِيَّة: <c>key</c> (terms/privacy/refund/…) +
/// <c>label</c> عَرَبي. الـ Razor تَفتَح المُحتَوى عَبر مَسار آخَر يَتَّفِق
/// مَع الـ key.</summary>
public sealed record LegalPageItem(string Key, string Label);

/// <summary>
/// مَنفَذ يُوَفِّر قائِمَة الصَفحات القانونِيَّة الَّتي يَعرِضها
/// <c>/legal</c>. الافتِراضي ٣: الشُروط، الخُصوصِيَّة، الاستِرداد.
/// التَطبيق يُسَجِّل impl خاصّاً لَو يَحتاج المَزيد أَو تَرجَمات.
/// </summary>
public interface ILegalPageProvider
{
    IReadOnlyList<LegalPageItem> List { get; }
}

/// <summary>تَنفيذ افتِراضي بِالقَوائم الثَلاث المُعتادَة.</summary>
public sealed class DefaultLegalPageProvider : ILegalPageProvider
{
    public IReadOnlyList<LegalPageItem> List { get; } = new[]
    {
        new LegalPageItem("terms",   "الشروط والأحكام"),
        new LegalPageItem("privacy", "سياسة الخصوصية"),
        new LegalPageItem("refund",  "سياسة الاسترداد"),
    };
}
