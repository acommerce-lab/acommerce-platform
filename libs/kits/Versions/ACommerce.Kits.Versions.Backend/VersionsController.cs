using ACommerce.Kits.Versions.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Mvc;

namespace ACommerce.Kits.Versions.Backend;

/// <summary>
/// متحكم العميل — يعرض <c>GET /version/check</c> فقط. مفتوح بدون توثيق لأنّ
/// التطبيق يحتاج استدعاءه قبل تسجيل الدخول لمعرفة هل هو محجوب أم لا.
///
/// <para>الـ endpoint يقبل المنصّة والإصدار من رؤوس <c>X-App-Platform</c> و
/// <c>X-App-Version</c>، أو من query params لو غاب الرأسان (مثلاً صفحة admin
/// تختبر النتيجة يدوياً).</para>
/// </summary>
[ApiController]
[Route("version")]
public sealed class VersionsController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly IAppVersionGate _gate;

    public VersionsController(OpEngine engine, IAppVersionGate gate)
    {
        _engine = engine;
        _gate = gate;
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check(
        [FromQuery] string? platform,
        [FromQuery] string? version,
        CancellationToken ct)
    {
        platform ??= Request.Headers[VersionTagKeys.PlatformHeader].ToString();
        version  ??= Request.Headers[VersionTagKeys.VersionHeader].ToString();
        if (string.IsNullOrWhiteSpace(platform)) platform = VersionTagKeys.DefaultPlatform;
        if (string.IsNullOrWhiteSpace(version))  version  = "0.0.0";

        var op = Entry.Create("version.check")
            .Describe($"Version check {platform}/{version}")
            .Tag(VersionTagKeys.SkipVersionGate, "true")
            .Tag("platform", platform).Tag("version", version)
            .Execute(async ctx =>
            {
                var check = await _gate.CheckAsync(platform, version, ctx.CancellationToken);
                ctx.Set("version_check_result", check);
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(
            op,
            ctx => ctx.Get<VersionCheckResult>("version_check_result"),
            ct);
        return Ok(env);
    }
}
