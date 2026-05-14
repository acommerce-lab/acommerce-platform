namespace ACommerce.Payments.Providers.Mock;

/// <summary>
/// خِيارات مُزَوِّد الدَفع الوَهمي. كُلّها قابِلَة لِلتَكوين مَن
/// appsettings أَو delegate في <c>AddMockPayment(opts => …)</c>.
/// </summary>
public sealed class MockPaymentOptions
{
    /// <summary>
    /// زَمَن ما بَين <c>InitiateAsync</c> و إعتِبار الدَفع ناجِحاً
    /// (مُحاكاة لِزَمَن إكمال المُستَخدِم في بَوّابَة حَقيقِيَّة). الافتِراضي
    /// <c>5</c> ثَوانٍ. مَع <c>0</c>، <c>GetStatusAsync</c> يُرجِع
    /// <c>Captured</c> فَوريّاً.
    /// </summary>
    public int AutoCaptureSeconds { get; set; } = 5;

    /// <summary>
    /// مَبلَغ يَجعَل المُحاكاة تَفشَل (لِاختِبار مَسار الخَطَأ). أَيّ مَبلَغ
    /// مُطابِق ⇒ <c>Initiate</c> يُرجِع <c>Succeeded=false</c>. الافتِراضي
    /// null (لا فَشَل).
    /// </summary>
    public decimal? FailOnAmount { get; set; }

    /// <summary>
    /// قاعِدَة URL لِصَفحَة الدَفع الوَهمِيَّة. الـ frontend يَفتَحها
    /// (أَو يُحاكي ضَغطَة "دَفعت" بَدَلاً). الـ <c>{ref}</c> يُستَبدَل بِـ
    /// reference الدَفع. الافتِراضي = relative path يَستَهلِكه التَطبيق
    /// المُضيف لِيَفتَح صَفحَة dev محَلِّيَّة.
    /// </summary>
    public string PaymentUrlTemplate { get; set; } = "/payments/mock/checkout?ref={ref}";
}
