using ACommerce.Kits.Reports.Domain;
using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Reports.Operations;

/// <summary>أنواع عمليّات Reports kit — typed.</summary>
public static class ReportOps
{
    public static readonly OperationType Submit    = new("report.submit");
    public static readonly OperationType SetStatus = new("report.set_status");
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


/// <summary>
/// عقد التخزين. التطبيق يُنفِّذه (مثل <c>EjarReportStore</c>) ويعرف شكل
/// DB الفعليّ. كلّ الـ <i>writes</i> اختياريّة — default no-op يجعل
/// <c>report.submit</c> ينجح كحدث OAM حتّى دون جدول Reports.
/// </summary>
public interface IReportStore
{
    /// <summary>إنشاء بلاغ جديد. مسار قديم — يُستخدم للتوافق فقط.</summary>
    [Obsolete("استعمل AddNoSaveAsync بدلاً منها — يقبل IReport مبنيّاً مسبقاً ولا يحفظ بنفسه (F6: SaveAtEnd على القيد).")]
    Task<IReport> SubmitAsync(
        string reporterId,
        string entityType,
        string entityId,
        string reason,
        string? body,
        CancellationToken ct);

    /// <summary>
    /// يُسجِّل البلاغ على tracker (F6: لا <c>SaveChangesAsync</c>).
    /// المُتّصِل (<c>ReportsController.Submit</c>) يبني <see cref="IReport"/>
    /// كـ POCO أوّلاً (<see cref="InMemoryReport"/>)، يضعه على
    /// <c>ctx.WithEntity&lt;IReport&gt;()</c> ثمّ يستدعي هذه الدالّة لو
    /// أراد persistence. default no-op يجعل العمليّة تنجح حتّى لو رفض
    /// التطبيق الفهرسة في DB.
    /// </summary>
    Task AddNoSaveAsync(IReport report, CancellationToken ct) => Task.CompletedTask;

    /// <summary>قائمة بلاغات هذا المستخدم — للـ "بلاغاتي".</summary>
    Task<IReadOnlyList<IReport>> ListMineAsync(string userId, CancellationToken ct);

    /// <summary>قائمة كلّ البلاغات (للوكيل/الإدارة) — مع فلترة اختياريّة بالحالة.</summary>
    Task<IReadOnlyList<IReport>> ListAllAsync(string? status, CancellationToken ct);

    /// <summary>تغيير الحالة. الوكيل/الإدارة فقط.</summary>
    Task<bool> SetStatusAsync(string reportId, string newStatus, CancellationToken ct);
}
