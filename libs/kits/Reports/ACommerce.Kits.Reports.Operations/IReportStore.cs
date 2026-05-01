using ACommerce.Kits.Reports.Domain;
using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Reports.Operations;

/// <summary>أنواع عمليّات Reports kit — typed.</summary>
public static class ReportOps
{
    public static readonly OperationType Submit    = new("report.submit");
    public static readonly OperationType SetStatus = new("report.set_status");
}

/// <summary>توافق خلفيّ.</summary>
public static class ReportOperationTypes
{
    public static readonly OperationType Submit    = ReportOps.Submit;
    public static readonly OperationType SetStatus = ReportOps.SetStatus;
}

/// <summary>مفاتيح وسوم البلاغات.</summary>
public static class ReportTagKeys
{
    public static readonly TagKey Kind       = new("kind");
    public static readonly TagKey EntityType = new("entity_type");
    public static readonly TagKey EntityId   = new("entity_id");
    public static readonly TagKey Reason     = new("reason");
    public static readonly TagKey FromStatus = new("from_status");
    public static readonly TagKey ToStatus   = new("to_status");
}

public static class ReportTagValues
{
    public static readonly TagValue Report = new("report");
}

public static class ReportMarkers
{
    public static readonly Marker IsReport = new(ReportTagKeys.Kind, ReportTagValues.Report);
}

/// <summary>توافق خلفيّ — مهجور.</summary>
public static class ReportTags
{
    public const string Kind       = "kind";
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
