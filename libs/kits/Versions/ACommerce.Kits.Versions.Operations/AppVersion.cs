namespace ACommerce.Kits.Versions.Operations;

/// <summary>
/// حالة إصدار التطبيق. كل حالة تحدّد سلوك الواجهة الأماميّة وقرار البوّابة الخلفيّة.
/// </summary>
public enum VersionStatus
{
    /// <summary>الأحدث — لا تنبيه ولا حجب.</summary>
    Latest = 0,

    /// <summary>نشط — يعمل بدون قيود لكنّ هناك إصداراً أحدث (تنبيه ناعم اختياريّ).</summary>
    Active = 1,

    /// <summary>قريب الإلغاء — تنبيه واضح للمستخدم بأنّ الدعم سينتهي قريباً.</summary>
    NearSunset = 2,

    /// <summary>ملغى الدعم — تحذير قويّ، الواجهة تظهر شريطاً دائماً، الخدمة لا تزال تستجيب.</summary>
    Deprecated = 3,

    /// <summary>غير مدعوم — حجب كامل: الواجهة تعرض صفحة الترقية، الخدمة ترفض الطلب.</summary>
    Unsupported = 4
}

/// <summary>
/// عقد إصدار. التطبيق يخزّنه كيف يشاء (DB/JSON/in-memory)، يكفي أن يطابق هذه الحقول.
///
/// <para><b>Platform</b>: مفتاح المنصّة — "web" / "mobile" / "admin" / أيّ مفتاح آخر
/// يتفق عليه التطبيق مع عميله. تُرسَل في رأس <c>X-App-Platform</c>.</para>
///
/// <para><b>Version</b>: سلسلة بصيغة semver-lite: "1.2.3". تُرسَل في رأس
/// <c>X-App-Version</c> ويُجرى تطبيع semver لمقارنة "أحدث/أقدم".</para>
///
/// <para><b>SunsetAt</b>: تاريخ نهاية الدعم. الواجهة قد تستخدمه لإظهار عدّ تنازليّ.</para>
/// </summary>
public sealed record AppVersion(
    string       Platform,
    string       Version,
    VersionStatus Status,
    DateTime?    SunsetAt = null,
    string?      Notes    = null,
    string?      DownloadUrl = null);

/// <summary>نتيجة فحص الإصدار التي تُرجَع للعميل وللمعترض.</summary>
public sealed record VersionCheckResult(
    string        Platform,
    string        Version,
    VersionStatus Status,
    string?       Latest,
    DateTime?     SunsetAt,
    string?       Notes,
    string?       DownloadUrl)
{
    /// <summary>هل يجب حجب التطبيق كاملاً؟</summary>
    public bool IsBlocked => Status == VersionStatus.Unsupported;

    /// <summary>هل يجب إظهار شريط/تنبيه ناعم؟</summary>
    public bool ShouldWarn => Status is VersionStatus.NearSunset or VersionStatus.Deprecated;

    /// <summary>هل هناك إصدار أحدث متاح؟</summary>
    public bool HasNewer =>
        !string.IsNullOrEmpty(Latest) && !string.Equals(Latest, Version, StringComparison.OrdinalIgnoreCase);
}
