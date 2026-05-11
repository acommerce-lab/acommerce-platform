namespace ACommerce.Authentication.TwoFactor.Providers.Nafath.Mock;

/// <summary>
/// خِيارات قَناة نَفاذ التَّجريبيَّة. يُحَدِّدها التَطبيق عِند التَسجيل
/// (<c>AddMockNafathTwoFactor</c>) — افتِراضِيّاتها مُناسِبَة لِغالِبيَّة
/// التَجارُب.
///
/// <para><b>أَين تُسَجَّل في appsettings</b>:</para>
/// <code>
/// "MockNafath": {
///   "DisplayCode": "00",
///   "AutoVerifySeconds": 5
/// }
/// </code>
/// ثُمّ في Program.cs:
/// <code>
/// services.AddMockNafathTwoFactor(builder.Configuration.GetSection("MockNafath"));
/// </code>
/// </summary>
public sealed class MockNafathOptions
{
    /// <summary>
    /// الرَّقَم الَّذي يَعرِضه الخادِم لِلمُستَخدِم بَعد بِدء التَّحَدّي.
    /// المُستَخدِم يَتَوَقَّع رُؤيَة هذا الرَّقَم في تَطبيق نَفاذ ويَضغَط
    /// عَليه. الافتِراضِي <c>"00"</c> لِسُهولَة الاختِبار اليَدَوي.
    /// </summary>
    public string DisplayCode { get; set; } = "00";

    /// <summary>
    /// عَدَد الثَّوانِي قَبل أَن يُرجِع <c>VerifyAsync</c> نَجاحاً تِلقائيّاً
    /// (مُحاكاة ضَغط المُستَخدِم في تَطبيق نَفاذ الحَقيقي). الافتِراضِي <c>10</c>.
    /// </summary>
    public int AutoVerifySeconds { get; set; } = 10;

    /// <summary>
    /// عَدَد الثَّوانِي قَبل أَن يَنتَهي صَلاحِيَّة التَّحَدّي (يُحاكي الانتِظار
    /// المَسموح في نَفاذ الحَقيقي). الافتِراضِي <c>120</c>.
    /// </summary>
    public int ExpirySeconds { get; set; } = 120;
}
