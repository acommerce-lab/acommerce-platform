namespace ACommerce.Kits.Versions.Operations;

/// <summary>
/// مفاتيح التاجات والـ HTTP headers المستخدمة من معترض الإصدارات.
/// الأسماء ثابتة على مستوى الـ Kit حتى لا يحتاج التطبيق لاتفاق منفصل.
/// </summary>
public static class VersionTagKeys
{
    /// <summary>اسم رأس الإصدار في طلب HTTP — يُرسله العميل في كلّ طلب.</summary>
    public const string VersionHeader  = "X-App-Version";

    /// <summary>اسم رأس المنصّة — قيم متّفق عليها مثل "web" / "mobile" / "admin".</summary>
    public const string PlatformHeader = "X-App-Platform";

    /// <summary>المنصّة الافتراضيّة لو لم يُرسلها العميل.</summary>
    public const string DefaultPlatform = "unknown";

    /// <summary>
    /// الـ tag الذي يُسَم به أيّ عملية يجب أن <b>يتجاوزها</b> معترض الإصدارات
    /// (لأنّها جزء من الفحص ذاته أو من بيانات الترقية). يُضاف تلقائياً على عمليّات
    /// <c>version.*</c> داخل الـ Kit؛ يستطيع التطبيق استخدامه على عمليّات إضافيّة
    /// (مثلاً تنزيل التطبيق) إن لزم.
    /// </summary>
    public const string SkipVersionGate = "skip_version_gate";

    /// <summary>أكواد رفض ثابتة للمعترض (كي تستطيع الواجهة فهمها وعرض صفحة الترقية).</summary>
    public const string RejectionCode_Unsupported   = "version_unsupported";
    public const string RejectionCode_MissingHeader = "version_header_missing";
}
