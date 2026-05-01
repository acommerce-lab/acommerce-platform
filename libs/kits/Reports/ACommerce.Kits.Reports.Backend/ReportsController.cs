using ACommerce.Kits.Reports.Domain;
using ACommerce.Kits.Reports.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Reports.Backend;

/// <summary>
/// نقاط نهاية البلاغات. كلّ عمليّة OAM-pure: <c>Entry.Create → Analyze →
/// Execute → ExecuteEnvelopeAsync</c>. الـ side effects (لو أردت إضافة
/// notification للإدارة عند بلاغ جديد) تُضاف لاحقاً عبر
/// <c>IOperationInterceptor</c> مسجَّل على <c>op.Type == "report.submit"</c>
/// — بدون لمس هذا الـ controller.
///
/// <para>لا ردود ولا محادثة في البلاغ — بخلاف Support kit. البلاغ "أبلِغ-وانسَ"
/// من جانب المستخدم؛ الإدارة تُغلِق أو تُرفض من جهتها عبر
/// <c>PATCH /reports/{id}/status</c>.</para>
///
/// <para>المسارات:
///   <c>POST   /reports</c>          — إرسال بلاغ.
///   <c>GET    /reports/me</c>       — بلاغاتي.
///   <c>GET    /reports/all</c>      — كلّ البلاغات (للإدارة).
///   <c>PATCH  /reports/{id}/status</c> — تغيير الحالة (للإدارة).
/// </para>
/// </summary>
[ApiController]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportStore _store;
    private readonly OpEngine _engine;
    private readonly ReportsKitOptions _options;

    public ReportsController(IReportStore store, OpEngine engine, ReportsKitOptions options)
    {
        _store = store; _engine = engine; _options = options;
    }

    private string CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("user_id missing from token");

    private string CallerPartyId => $"{_options.PartyKind}:{CallerId}";

    // ─── POST /reports ─────────────────────────────────────────────────
    public sealed record SubmitRequest(string? EntityType, string? EntityId, string? Reason, string? Body);

    [HttpPost("/reports")]
    public async Task<IActionResult> Submit([FromBody] SubmitRequest req, CancellationToken ct)
    {
        IReport? created = null;
        var op = Entry.Create(ReportOps.Submit)
            .Describe($"User {CallerId} reports {req.EntityType}:{req.EntityId}")
            .From(CallerPartyId, 1, ("role", "reporter"))
            .To($"{req.EntityType}:{req.EntityId}", 1, ("role", "reported"))
            .Mark(ReportMarkers.IsReport)
            .Tag(ReportTagKeys.EntityType, req.EntityType ?? "")
            .Tag(ReportTagKeys.EntityId,   req.EntityId ?? "")
            .Tag(ReportTagKeys.Reason,     req.Reason ?? "")
            .Analyze(new RequiredFieldAnalyzer("entity_type", () => req.EntityType))
            .Analyze(new RequiredFieldAnalyzer("entity_id",   () => req.EntityId))
            .Analyze(new RequiredFieldAnalyzer("reason",      () => req.Reason))
            .Analyze(new MaxLengthAnalyzer ("body",         () => req.Body, 1000))
            .Analyze(new PredicateAnalyzer("valid_reason",
                (Func<OperationContext, AnalyzerResult>)(_ =>
                    ReportReasons.IsValid(req.Reason)
                        ? AnalyzerResult.Pass()
                        : AnalyzerResult.Fail($"سبب غير معروف: {req.Reason}"))))
            .Execute(async ctx =>
            {
                created = await _store.SubmitAsync(
                    reporterId: CallerId,
                    entityType: req.EntityType!,
                    entityId:   req.EntityId!,
                    reason:     req.Reason!,
                    body:       req.Body,
                    ct:         ctx.CancellationToken);
            })
            .SaveAtEnd()  // F6
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (object?)created ?? new { }, ct);
        if (env.Operation.Status != "Success" || created is null)
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "submit_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope(ReportOps.Submit, created);
    }

    // ─── GET /reports/me ───────────────────────────────────────────────
    [HttpGet("/reports/me")]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        var rows = await _store.ListMineAsync(CallerId, ct);
        return this.OkEnvelope("report.list_mine", rows);
    }

    // ─── GET /reports/all ──────────────────────────────────────────────
    // ملاحظة: هذا للإدارة. الـ guard المناسب يضاف من جهة التطبيق عبر
    // [Authorize(Roles="admin,agent")] لو احتاجت Ejar تمييز أدوار.
    [HttpGet("/reports/all")]
    public async Task<IActionResult> ListAll([FromQuery] string? status, CancellationToken ct)
    {
        var rows = await _store.ListAllAsync(status, ct);
        return this.OkEnvelope("report.list_all", rows);
    }

    // ─── PATCH /reports/{id}/status ────────────────────────────────────
    public sealed record StatusRequest(string? Status);

    [HttpPatch("/reports/{id}/status")]
    public async Task<IActionResult> SetStatus(string id, [FromBody] StatusRequest req, CancellationToken ct)
    {
        var newStatus = (req.Status ?? "").Trim().ToLowerInvariant();
        if (newStatus is not ("open" or "reviewing" or "resolved" or "dismissed"))
            return this.BadRequestEnvelope("invalid_status");

        var op = Entry.Create(ReportOps.SetStatus)
            .Describe($"Report {id} → {newStatus}")
            .From(CallerPartyId, 1, ("role", "actor"))
            .To($"Report:{id}",  1, ("role", "status_updated"))
            .Mark(ReportMarkers.IsReport)
            .Tag(ReportTagKeys.ToStatus, newStatus)
            .Execute(async ctx =>
            {
                await _store.SetStatusAsync(id, newStatus, ctx.CancellationToken);
            })
            .SaveAtEnd()  // F6
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id, status = newStatus }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "status_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope(ReportOps.SetStatus, new { id, status = newStatus });
    }

    // ─── GET /reports/reasons ──────────────────────────────────────────
    // للواجهة لعرض قائمة الأسباب — يكفي ثابت (لا يتغيّر بين النشرات
    // الكبيرة)، لكن نُعرضه عبر endpoint ليكون i18n-able مستقبلاً.
    [HttpGet("/reports/reasons")]
    [AllowAnonymous]
    public IActionResult Reasons() =>
        this.OkEnvelope("report.reasons", ReportReasons.All);
}
