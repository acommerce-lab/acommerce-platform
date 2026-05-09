namespace ACommerce.Kits.Versions.Operations;

/// <summary>
/// منفذ قرار الإصدار على جانب الخادم. التطبيق يحقن تطبيقاً ملموساً (يقرأ من DB
/// أو ملفّ JSON أو إعدادات). ما يهمّ الـ Kit: عند طلب الفحص، نعرف الحالة + الأحدث.
///
/// <para>الفصل عن <c>IVersionStore</c> مقصود: <c>IVersionStore</c> (في الـ Backend)
/// هو سطح إدارة CRUD كامل للإصدارات. <c>IAppVersionGate</c> هو واجهة قراءة فقط
/// مُبسَّطة يستخدمها المعترض في كلّ طلب — نفصلها لإمكانيّة وضع cache بسيط
/// أمامها دون كشف عمليّات الإدارة.</para>
/// </summary>
public interface IAppVersionGate
{
    /// <summary>
    /// يقرّر حالة إصدار العميل. يرجع <c>VersionCheckResult</c> يصف الحالة + الأحدث + روابط الترقية.
    /// إذا كان الـ platform/version مجهولاً تماماً يستطيع المنفذ معاملته كـ <c>Unsupported</c>
    /// أو الافتراضي حسب سياسة التطبيق.
    /// </summary>
    Task<VersionCheckResult> CheckAsync(string platform, string version, CancellationToken ct);
}
