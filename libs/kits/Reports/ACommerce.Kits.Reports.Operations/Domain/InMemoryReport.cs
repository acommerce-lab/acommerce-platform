namespace ACommerce.Kits.Reports.Domain;

/// <summary>
/// تطبيق POCO نقيّ لـ <see cref="IReport"/> — لا EF، لا DB. يُستخدم في
/// <c>ReportsController.Submit</c> كحدث OAM أصيل: الـ Execute body يبنيه
/// ويضعه على <c>ctx.WithEntity&lt;IReport&gt;()</c> فيتدفّق لكلّ
/// post-interceptor (notify-moderation، email-team، slack-alert، …)
/// مستقلّاً عن وجود جدول <c>Reports</c> في DB.
///
/// <para>الفكرة المعماريّة (مرآة <see cref="ACommerce.Chat.Operations.InMemoryChatMessage"/>):
/// البلاغ كحدث منفصل عن تخزينه. تطبيق يستطيع إسقاط <c>IReportStore</c> أو
/// تركيب persistence لاحقاً — العمليّة <c>report.submit</c> لا تكسر.</para>
/// </summary>
public sealed record InMemoryReport(
    string Id,
    string ReporterId,
    string EntityType,
    string EntityId,
    string Reason,
    string? Body,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null
) : IReport;
