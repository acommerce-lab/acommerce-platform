using ACommerce.Authentication.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Ejar.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ejar.Admin.Api.Controllers;

/// <summary>
/// SMS OTP auth for admin login. Mirror of <c>Ejar.Api.Controllers.AuthController</c>;
/// the only differences are the JWT config (separate issuer/audience) and the
/// <c>role=admin</c> claim that backend authorization uses.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly EjarAdminJwtConfig _jwt;
    private readonly ITwoFactorChannel _otpChannel;

    public AuthController(OpEngine engine, EjarAdminJwtConfig jwt, ITwoFactorChannel otpChannel)
    {
        _engine = engine; _jwt = jwt; _otpChannel = otpChannel;
    }

    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequestBody body, CancellationToken ct)
    {
        var phone = PhoneNormalization.Normalize(body.Phone);
        var op = Entry.Create("auth.otp.request")
            .From("System:Auth", 1, ("role", "issuer"))
            .To($"Phone:{phone}", 1, ("role", "recipient"))
            .Tag("phone_masked", MaskPhone(phone))
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Analyze(new ConditionAnalyzer("phone_format",
                _ => phone.Length >= 10 && phone.StartsWith('+') && phone[1..].All(char.IsDigit),
                "رقم الجوال غير صالح"))
            .Execute(async ctx => await _otpChannel.InitiateAsync(phone, phone, ctx.CancellationToken))
            .Build();
        var data = new { maskedPhone = MaskPhone(phone), expiresInSeconds = 120 };
        var env = await _engine.ExecuteEnvelopeAsync(op, data, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "otp_request_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.otp.request", data);
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyBody body, CancellationToken ct)
    {
        var phone = PhoneNormalization.Normalize(body.Phone);
        var code  = body.Code?.Trim() ?? "";
        // Reuses the SHARED EjarSeed user pool — a phone that exists as a renter is
        // also valid as a provider; their role is just an additional claim.
        var userId = EjarSeed.GetOrCreateUserId(phone);
        var user   = EjarSeed.GetUser(userId);
        var token  = GenerateToken(userId, phone);

        var op = Entry.Create("auth.otp.verify")
            .From($"Phone:{phone}", 1, ("role", "verifier"))
            .To($"Admin:{userId}", 1, ("role", "authenticated"))
            .Tag("user_id", userId).Tag("role", "admin")
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Analyze(new RequiredFieldAnalyzer("code",  () => code))
            .Analyze(new ConditionAnalyzer("code_length",
                _ => code.Length == 6 && code.All(char.IsDigit),
                "رمز التحقق يجب أن يكون 6 أرقام"))
            .Execute(async ctx =>
            {
                var r = await _otpChannel.VerifyAsync(phone, code, ctx.CancellationToken);
                if (!r.Verified) throw new InvalidOperationException(r.Reason ?? "wrong_code");
            })
            .Build();

        var data = new { token, userId, name = user?.FullName ?? "", phone = MaskPhone(phone), role = "admin" };
        var env = await _engine.ExecuteEnvelopeAsync(op, data, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "otp_verify_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.otp.verify", data);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var userId = HttpContext.Items["user_id"] as string ?? EjarSeed.CurrentUserId;
        return this.OkEnvelope("auth.logout", new { userId });
    }

    private string GenerateToken(string userId, string phone)
    {
        var claims = new[]
        {
            new Claim("sub",     userId),
            new Claim("user_id", userId),
            new Claim("phone",   phone),
            new Claim("role",    "admin"),
        };
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims,
            expires: DateTime.UtcNow.AddDays(30), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string MaskPhone(string p) =>
        p.Length < 4 ? "****" : new string('*', p.Length - 4) + p[^4..];

    public sealed record OtpRequestBody(string? Phone);
    public sealed record OtpVerifyBody(string? Phone, string? Code);
}
