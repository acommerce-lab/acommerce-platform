using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Ashare.V2.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ashare.V2.Api.Controllers;

/// <summary>
/// مصادقة نفاذ — يستخدم ITwoFactorChannel المُحقَن (محاكاة: تحقق تلقائي بعد 10 ثوانٍ).
/// عند التبديل للإنتاج: أبدل مكتبة Providers.Nafath.Mock بـ Providers.Nafath الحقيقية.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly ITwoFactorChannel _nafathChannel;
    private readonly AshareV2JwtConfig _jwt;

    // challengeId → nationalId (في الذاكرة — كافٍ للمحاكاة)
    private static readonly Dictionary<string, string> _challengeToId = new();
    private static readonly object _challengeLock = new();

    private string CurrentUserId => HttpContext.Items["user_id"] as string ?? AshareV2Seed.CurrentUserId;

    public AuthController(OpEngine engine, ITwoFactorChannel nafathChannel, AshareV2JwtConfig jwt)
    {
        _engine        = engine;
        _nafathChannel = nafathChannel;
        _jwt           = jwt;
    }

    // ─── POST /auth/nafath/start ────────────────────────────────────────
    [HttpPost("nafath/start")]
    public async Task<IActionResult> NafathStart([FromBody] NafathStartBody body, CancellationToken ct)
    {
        var nationalId = body.NationalId?.Trim() ?? string.Empty;

        string? challengeId = null;
        string? displayCode = null;

        var op = Entry.Create("auth.nafath.start")
            .Describe($"Start Nafath for {MaskId(nationalId)}")
            .From("System:Auth", 1, ("role", "issuer"))
            .To($"NationalId:{MaskId(nationalId)}", 1, ("role", "recipient"))
            .Tag("national_id_masked", MaskId(nationalId))
            .Analyze(new RequiredFieldAnalyzer("nationalId", () => nationalId))
            .Analyze(new ConditionAnalyzer("national_id_length",
                _ => nationalId.Length == 10 && nationalId.All(char.IsDigit),
                "رقم الهوية يجب أن يكون 10 أرقام"))
            .Execute(async ctx =>
            {
                var result = await _nafathChannel.InitiateAsync(nationalId, null, ctx.CancellationToken);
                if (!result.Succeeded)
                    throw new InvalidOperationException(result.Error ?? "initiate_failed");

                challengeId = result.ChallengeId;
                displayCode = result.ProviderData?["displayCode"] ?? "00";
                lock (_challengeLock) _challengeToId[challengeId] = nationalId;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "nafath_start_failed",
                                           env.Operation.ErrorMessage);

        return this.OkEnvelope("auth.nafath.start", new
        {
            challengeId,
            displayCode,
            expiresInSeconds = 120
        });
    }

    // ─── GET /auth/nafath/status/{challengeId} ──────────────────────────
    [HttpGet("nafath/status/{challengeId}")]
    public async Task<IActionResult> NafathStatus(string challengeId, CancellationToken ct)
    {
        string? nationalId;
        lock (_challengeLock) _challengeToId.TryGetValue(challengeId, out nationalId);

        if (nationalId == null)
            return this.NotFoundEnvelope("auth.nafath.status", "Challenge not found");

        var result = await _nafathChannel.VerifyAsync(challengeId, null, ct);

        if (result.Verified)
        {
            var userId = AshareV2Seed.CurrentUserId;
            var name   = AshareV2Seed.Profile.FullName;
            var token  = GenerateToken(userId, nationalId);
            lock (_challengeLock) _challengeToId.Remove(challengeId);
            return this.OkEnvelope("auth.nafath.verify", new
            {
                verified = true,
                token,
                userId,
                name
            });
        }

        var pending = result.Reason?.StartsWith("pending:") == true
            ? int.TryParse(result.Reason[8..], out var s) ? s : 10
            : 10;

        return this.OkEnvelope("auth.nafath.status", new { verified = false, pendingSeconds = pending });
    }

    // ─── POST /auth/logout ──────────────────────────────────────────────
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = CurrentUserId;

        var op = Entry.Create("auth.logout")
            .Describe($"User {userId} logs out")
            .From($"User:{userId}", 1, ("role", "actor"))
            .To("System:Auth", -1, ("role", "session"))
            .Tag("user_id", userId)
            .Execute(_ => Task.CompletedTask)
            .Build();

        var responseData = new { userId };
        var env = await _engine.ExecuteEnvelopeAsync(op, responseData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "logout_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.logout", responseData);
    }

    // ─── helpers ────────────────────────────────────────────────────────
    private string GenerateToken(string userId, string nationalId)
    {
        var claims = new[]
        {
            new Claim("sub",         userId),
            new Claim("user_id",     userId),
            new Claim("national_id", MaskId(nationalId))
        };
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _jwt.Issuer, _jwt.Audience, claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string MaskId(string id) =>
        id.Length < 4 ? "****" : new string('*', id.Length - 4) + id[^4..];

    public sealed record NafathStartBody(string? NationalId);
}
