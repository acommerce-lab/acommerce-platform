namespace ACommerce.Payments.Operations.Abstractions;

/// <summary>
/// Capability mixin — مَن يُنَفِّذها يُصَرِّح أَنّه يَدعَم Apple Pay
/// (Wallet) كَطَريقَة دَفع بِجانِب الواجِهَة العامَّة <see cref="IPaymentGateway"/>.
///
/// <para><b>نَمَط الاستِخدام</b>: التَطبيق يَطلُب <c>IEnumerable&lt;IPaymentGateway&gt;</c>
/// مَن DI. لِكَشف Apple Pay، يَفلتُره عَبر <c>OfType&lt;IAppleWalletGateway&gt;()</c>.
/// إن وُجِد + الجِهاز iOS/Safari ⇒ يَعرِض زَرّ Apple Pay. غَير ذلك ⇒
/// يَتَجاوَزه ويَستَخدِم الـ flow العامّ.</para>
///
/// <para><b>المُزَوِّدون المُحتَمَلون</b>: Stripe، Moyasar (دَعم رَسمي)،
/// HyperPay، Tap. كُلٌّ يُنَفِّذ كِلا الواجِهَتَين. مُزَوِّد لا يَدعَم Apple Pay
/// يُنَفِّذ <see cref="IPaymentGateway"/> فَقَط.</para>
///
/// <para><b>الـ Mock</b>: في dev، MockPaymentProvider يُنَفِّذ هذه الواجِهَة
/// عَلى iOS فَقَط (يَكشِف عَبر User-Agent) ⇒ يُحاكي Apple Pay sheet بِلا
/// API حَقيقي. هذا يَسمَح بِاختِبار الـ UX بِلا بَطاقَة فِعلِيَّة.</para>
/// </summary>
public interface IAppleWalletGateway : IPaymentGateway
{
    /// <summary>
    /// يَبدأ Apple Pay session — يَرُدّ merchant validation payload الَّذي
    /// تُمَرِّره الواجِهَة لِـ ApplePayJS API لِفَتح الـ sheet عَلى الجِهاز.
    ///
    /// <para><b>تَدَفُّق التَطبيق</b>:
    /// <list type="number">
    ///   <item>المُستَخدِم يَضغَط "ادفَع بِـ Apple Pay" عَلى الواجِهَة.</item>
    ///   <item>الواجِهَة تُنشِئ <c>ApplePaySession</c> عَبر JS API.</item>
    ///   <item>عِندَ <c>onvalidatemerchant</c>، تَستَدعي backend الَّذي
    ///         بِدَوره يَستَدعي هذه الدالَّة + يُمَرِّر النَتيجَة لِلواجِهَة.</item>
    ///   <item>المُستَخدِم يُؤَكِّد عَلى الجِهاز، الـ JS يُرسِل
    ///         <c>ApplePayPaymentToken</c> إلى backend.</item>
    ///   <item>backend يَستَدعي <see cref="CompleteApplePaymentAsync"/>
    ///         لِلتَّحصيل الفِعلي.</item>
    /// </list></para>
    /// </summary>
    Task<AppleMerchantSession> StartAppleSessionAsync(
        AppleSessionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// يَستَكمِل الدَّفع بِاستِخدام token مَن ApplePayJS. مُكافِئ
    /// <see cref="IPaymentGateway.InitiateAsync"/> + capture في خُطوَة
    /// واحِدَة، لِأَنّ Apple Pay tokens صالِحَة لِـ charge فَوري.
    /// </summary>
    Task<InitiateResult> CompleteApplePaymentAsync(
        AppleCompleteRequest request,
        CancellationToken ct = default);
}

/// <summary>طَلَب بَدء Apple Pay session (مَلكَم Apple validation).</summary>
public record AppleSessionRequest(
    string MerchantIdentifier,
    string DisplayName,
    string DomainName,
    string ValidationUrl,
    decimal Amount,
    string Currency,
    string OrderReference);

/// <summary>نَتيجَة merchant validation — تُمَرَّر لِـ ApplePaySession.completeMerchantValidation.</summary>
public record AppleMerchantSession(
    bool   Succeeded,
    string? OpaqueData,
    string? Error = null);

/// <summary>طَلَب إتمام الدَّفع بَعد مُوافَقَة المُستَخدِم في الـ sheet.</summary>
public record AppleCompleteRequest(
    string  PaymentTokenJson,
    decimal Amount,
    string  Currency,
    string  OrderReference,
    string? CustomerEmail = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
