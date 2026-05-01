using ACommerce.Kits.Reports.Domain;

namespace ACommerce.Kits.Reports.Operations;

/// <summary>
/// أنواع عمليّات Reports kit (OAM Type strings).
/// </summary>
public static class ReportOperationTypes
{
    /// <summary>إنشاء بلاغ جديد.</summary>
    public const string Submit = "report.submit";

    /// <summary>تغيير حالة البلاغ (للإدارة).</summary>
    public const string SetStatus = "report.set_status";
}

/// <summary>
/// أوسمة OAM المعياريّة لعمليّات البلاغات.
/// </summary>
public static class ReportTags
{
    public const string Kind = "kind";
    public const string KindReport = "report";
    public const string EntityType = "entity_type";
    public const string EntityId   = "entity_id";
    public const string Reason     = "reason";
    public const string FromStatus = "from_status";
    public const string ToStatus   = "to_status";
}

/// <summary>
/// عقد التخزين. التطبيق يُنفِّذه (مثل <c>EjarReportStore</c>) ويعرف شكل
/// DB الفعليّ.
/// </summary>
public interface IReportStore
{
    /// <summary>إنشاء بلاغ جديد. الـ controller يستدعيها داخل OAM envelope.</summary>
    Task<IReport> SubmitAsync(
        string reporterId,
        string entityType,
        string entityId,
        string reason,
        string? body,
        CancellationToken ct);

    /// <summary>قائمة بلاغات هذا المستخدم — للـ "بلاغاتي".</summary>
    Task<IReadOnlyList<IReport>> ListMineAsync(string userId, CancellationToken ct);

    /// <summary>قائمة كلّ البلاغات (للوكيل/الإدارة) — مع فلترة اختياريّة بالحالة.</summary>
    Task<IReadOnlyList<IReport>> ListAllAsync(string? status, CancellationToken ct);

    /// <summary>تغيير الحالة. الوكيل/الإدارة فقط.</summary>
    Task<bool> SetStatusAsync(string reportId, string newStatus, CancellationToken ct);
}
