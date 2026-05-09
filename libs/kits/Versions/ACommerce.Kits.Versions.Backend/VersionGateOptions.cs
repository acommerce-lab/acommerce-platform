namespace ACommerce.Kits.Versions.Backend;

/// <summary>
/// خيارات بوّابة الإصدار — يحقنها التطبيق ضمن DI عبر <see cref="VersionsKitExtensions.AddVersionsKit"/>.
/// </summary>
public sealed class VersionGateOptions
{
    /// <summary>
    /// ماذا نفعل لو وصلنا طلب بإصدار غير مسجَّل في المخزن؟
    /// <list type="bullet">
    ///   <item><see cref="UnknownVersionPolicy.Lenient"/> (الافتراضيّ) — نعامله كـ
    ///         <see cref="Operations.VersionStatus.Active"/>. مناسب للمراحل الأولى:
    ///         نقبل أيّ إصدار قديم لأنّ المستخدمين قد لم يُحدّثوا بعد، ثمّ نشدّد
    ///         تدريجياً عبر تسجيل الإصدارات القديمة بحالات Deprecated/Unsupported.</item>
    ///   <item><see cref="UnknownVersionPolicy.Strict"/> — نعامله كـ
    ///         <see cref="Operations.VersionStatus.Unsupported"/>. مناسب للأنظمة
    ///         الناضجة حيث كلّ الإصدارات المنشورة معروفة، وأيّ شيء آخر يُحجَب.</item>
    /// </list>
    /// </summary>
    public UnknownVersionPolicy UnknownVersionPolicy { get; init; } = UnknownVersionPolicy.Lenient;
}

public enum UnknownVersionPolicy { Lenient, Strict }
