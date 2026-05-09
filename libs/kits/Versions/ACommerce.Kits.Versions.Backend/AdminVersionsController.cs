using ACommerce.Kits.Versions.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACommerce.Kits.Versions.Backend;

/// <summary>
/// متحكم إدارة الإصدارات — للأدمن فقط (يستخدم سياسة AuthZ من التطبيق).
/// المسارات: <c>GET /admin/versions</c>، <c>POST /admin/versions</c> (upsert)،
/// <c>POST /admin/versions/{platform}/{version}/status</c>، <c>DELETE /admin/versions/{platform}/{version}</c>.
///
/// <para>كلّ العمليّات تحمل اسم <c>version.*</c> فيتجاوزها معترض البوّابة تلقائياً
/// (لأنّ الأدمن قد يستخدم تطبيقاً قديماً ويحتاج للوصول لإصلاح الإصدارات).</para>
/// </summary>
[ApiController]
[Authorize(Policy = VersionsKitPolicies.Admin)]
[Route("admin/versions")]
public sealed class AdminVersionsController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly IVersionStore _store;

    public AdminVersionsController(OpEngine engine, IVersionStore store)
    {
        _engine = engine;
        _store = store;
    }

    public sealed record UpsertBody(
        string?  Platform,
        string?  Version,
        VersionStatus Status,
        DateTime? SunsetAt,
        string?  Notes,
        string?  DownloadUrl);

    public sealed record SetStatusBody(VersionStatus Status, DateTime? SunsetAt);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? platform, CancellationToken ct)
    {
        var rows = await _store.ListAsync(platform, ct);
        return this.OkEnvelope("version.list", rows);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertBody body, CancellationToken ct)
    {
        AppVersion? saved = null;
        var op = Entry.Create("version.upsert")
            .Describe($"Upsert version {body.Platform}/{body.Version}")
            .Tag(VersionTagKeys.SkipVersionGate, "true")
            .Tag("platform", body.Platform ?? "").Tag("version", body.Version ?? "")
            .Analyze(new RequiredFieldAnalyzer("platform", () => body.Platform))
            .Analyze(new RequiredFieldAnalyzer("version",  () => body.Version))
            .Execute(async ctx =>
            {
                saved = await _store.UpsertAsync(
                    new AppVersion(body.Platform!, body.Version!, body.Status,
                        body.SunsetAt, body.Notes, body.DownloadUrl),
                    ctx.CancellationToken);
            })
            .SaveAtEnd()  // F6: demote-prior-Latest + insert/update الجديد ذرّيّاً
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (object?)saved ?? new { }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(
                env.Operation.FailedAnalyzer ?? "version_upsert_failed",
                env.Operation.ErrorMessage);
        return this.OkEnvelope("version.upsert", saved);
    }

    [HttpPost("{platform}/{version}/status")]
    public async Task<IActionResult> SetStatus(
        string platform, string version,
        [FromBody] SetStatusBody body, CancellationToken ct)
    {
        var ok = false;
        var op = Entry.Create("version.set_status")
            .Describe($"Set status {platform}/{version} → {body.Status}")
            .Tag(VersionTagKeys.SkipVersionGate, "true")
            .Tag("platform", platform).Tag("version", version)
            .Tag("status", body.Status.ToString())
            .Execute(async ctx =>
            {
                ok = await _store.SetStatusAsync(platform, version, body.Status, body.SunsetAt, ctx.CancellationToken);
            })
            .SaveAtEnd()  // F6: demote-prior-Latest + status update ذرّيّاً
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op,
            new { platform, version, status = body.Status, sunsetAt = body.SunsetAt }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(
                env.Operation.FailedAnalyzer ?? "version_set_status_failed",
                env.Operation.ErrorMessage);
        if (!ok) return this.NotFoundEnvelope("version_not_found");
        return this.OkEnvelope("version.set_status",
            new { platform, version, status = body.Status, sunsetAt = body.SunsetAt });
    }

    [HttpDelete("{platform}/{version}")]
    public async Task<IActionResult> Delete(string platform, string version, CancellationToken ct)
    {
        var ok = false;
        var op = Entry.Create("version.delete")
            .Describe($"Delete version {platform}/{version}")
            .Tag(VersionTagKeys.SkipVersionGate, "true")
            .Tag("platform", platform).Tag("version", version)
            .Execute(async ctx =>
            {
                ok = await _store.DeleteAsync(platform, version, ctx.CancellationToken);
            })
            .SaveAtEnd()  // F6: soft-delete tracked-mutation يحفظ ذرّيّاً
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { platform, version }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(
                env.Operation.FailedAnalyzer ?? "version_delete_failed",
                env.Operation.ErrorMessage);
        if (!ok) return this.NotFoundEnvelope("version_not_found");
        return this.OkEnvelope("version.delete", new { platform, version });
    }
}
