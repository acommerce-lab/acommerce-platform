using ACommerce.Kits.Auth.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ACommerce.Kits.Auth.Backend;

/// <summary>
/// Drop-in auth controller. Drives an <see cref="IAuthFlow"/> — does NOT
/// know how the verification works (OTP, magic-link, WebAuthn…). Apps wire
/// a concrete IAuthFlow via DI:
/// <list type="bullet">
///   <item>OTP-via-2FA (most common) → <c>AddTwoFactorAsAuth()</c> from the
///         <c>Auth.TwoFactor.AsAuth</c> bridge package.</item>
///   <item>Email magic-link → an app-provided IAuthFlow that emails a token.</item>
///   <item>Custom flow → any IAuthFlow implementation.</item>
/// </list>
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly IAuthFlow _flow;
    private readonly IAuthUserStore _users;
    private readonly AuthKitJwtConfig _jwt;

    public AuthController(
        OpEngine engine, IAuthFlow flow, IAuthUserStore users, AuthKitJwtConfig jwt)
    {
        _engine = engine; _flow = flow; _users = users; _jwt = jwt;
    }

    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequestBody body, CancellationToken ct)
    {
        var subject = (body.Phone ?? "").Trim();
        var op = Entry.Create("auth.otp.request")
            .Describe($"Auth flow initiated for {Mask(subject)}")
            .From("System:Auth", 1, ("role", "issuer"))
            .To($"Subject:{subject}", 1, ("role", "recipient"))
            .Tag("subject_masked", Mask(subject))
            .Tag("role", _jwt.Role)
            .Analyze(new RequiredFieldAnalyzer("subject", () => subject))
            .Execute(async ctx =>
            {
                var r = await _flow.InitiateAsync(subject, ctx.CancellationToken);
                if (!r.Ok) throw new InvalidOperationException(r.Reason ?? "initiate_failed");
                ctx.Set("expiresInSeconds", r.ExpiresInSeconds);
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { masked = Mask(subject) }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "otp_request_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.otp.request",
            new { masked = Mask(subject), expiresInSeconds = (int)(env.Operation.GetVariable("expiresInSeconds") ?? 0) });
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyBody body, CancellationToken ct)
    {
        var subject = (body.Phone ?? "").Trim();
        var secret  = body.Code?.Trim() ?? "";

        string userId = "", displayName = "", token = "";

        var op = Entry.Create("auth.otp.verify")
            .Describe($"Auth flow completed for {Mask(subject)}")
            .From($"Subject:{subject}", 1, ("role", "verifier"))
            .To("System:Auth", 1, ("role", "authenticated"))
            .Tag("subject_masked", Mask(subject)).Tag("role", _jwt.Role)
            .Analyze(new RequiredFieldAnalyzer("subject", () => subject))
            .Analyze(new RequiredFieldAnalyzer("secret",  () => secret))
            .Execute(async ctx =>
            {
                var r = await _flow.CompleteAsync(subject, secret, ctx.CancellationToken);
                if (!r.Verified) throw new InvalidOperationException(r.Reason ?? "wrong_code");

                var resolvedSubject = string.IsNullOrEmpty(r.Subject) ? subject : r.Subject;
                userId      = await _users.GetOrCreateUserIdAsync(resolvedSubject, ctx.CancellationToken);
                displayName = await _users.GetDisplayNameAsync(userId, ctx.CancellationToken) ?? "";
                token       = GenerateToken(userId, resolvedSubject);
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { subject = Mask(subject) }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "otp_verify_failed", env.Operation.ErrorMessage);

        return this.OkEnvelope("auth.otp.verify",
            new { token, userId, name = displayName, phone = Mask(subject), role = _jwt.Role });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var userId = User.FindFirst("user_id")?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? "";
        return this.OkEnvelope("auth.logout", new { userId });
    }

    private string GenerateToken(string userId, string subject)
    {
        var claims = new[]
        {
            new Claim("sub",     userId),
            new Claim("user_id", userId),
            new Claim("subject", subject),
            new Claim("role",    _jwt.Role),
        };
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims,
            expires: DateTime.UtcNow.AddDays(_jwt.AccessTokenLifetimeDays),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Mask(string s) =>
        s.Length < 4 ? "****" : new string('*', s.Length - 4) + s[^4..];

    public sealed record OtpRequestBody(string? Phone);
    public sealed record OtpVerifyBody(string? Phone, string? Code);
}
