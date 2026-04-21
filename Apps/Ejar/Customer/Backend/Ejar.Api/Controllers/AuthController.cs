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

namespace Ejar.Api.Controllers;

/// <summary>
/// مصادقة SMS OTP — يستخدم ITwoFactorChannel المُحقَن (محاكاة: رمز 123456).
/// عند التبديل للإنتاج: أبدل مكتبة Providers.Sms.Mock بـ Providers.Sms الحقيقية.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly EjarJwtConfig _jwt;
    private readonly ITwoFactorChannel _otpChannel;

    public AuthController(OpEngine engine, EjarJwtConfig jwt, ITwoFactorChannel otpChannel)
    {
        _engine     = engine;
        _jwt        = jwt;
        _otpChannel = otpChannel;
    }

    // ─── POST /auth/otp/request ─────────────────────────────────────────
    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequestBody body, CancellationToken ct)
    {
        var phone = body.Phone?.Trim() ?? string.Empty;

        var op = Entry.Create("auth.otp.request")
            .Describe($"OTP requested for {MaskPhone(phone)}")
            .From("System:Auth", 1, ("role", "issuer"))
            .To($"Phone:{phone}", 1, ("role", "recipient"))
            .Tag("phone_masked", MaskPhone(phone))
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Analyze(new ConditionAnalyzer("phone_format",
                _ => phone.Length >= 9 && phone.All(c => char.IsDigit(c) || c == '+'),
                "رقم الجوال غير صالح"))
            .Execute(async ctx =>
            {
                await _otpChannel.InitiateAsync(phone, phone, ctx.CancellationToken);
            })
            .Build();

        var responseData = new { maskedPhone = MaskPhone(phone), expiresInSeconds = 120 };
        var env = await _engine.ExecuteEnvelopeAsync(op, responseData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "otp_request_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.otp.request", responseData);
    }

    // ─── POST /auth/otp/verify ──────────────────────────────────────────
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyBody body, CancellationToken ct)
    {
        var phone = body.Phone?.Trim() ?? string.Empty;
        var code  = body.Code?.Trim()  ?? string.Empty;

        var userId = EjarSeed.GetOrCreateUserId(phone);
        var user   = EjarSeed.GetUser(userId);
        var token  = GenerateToken(userId, phone);

        var op = Entry.Create("auth.otp.verify")
            .Describe($"OTP verify for {MaskPhone(phone)}")
            .From($"Phone:{phone}", 1, ("role", "verifier"))
            .To($"User:{userId}", 1, ("role", "authenticated"))
            .Tag("phone_masked", MaskPhone(phone))
            .Tag("user_id", userId)
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Analyze(new RequiredFieldAnalyzer("code",  () => code))
            .Analyze(new ConditionAnalyzer("code_length",
                _ => code.Length == 6 && code.All(char.IsDigit),
                "رمز التحقق يجب أن يكون 6 أرقام"))
            .Execute(async ctx =>
            {
                var result = await _otpChannel.VerifyAsync(phone, code, ctx.CancellationToken);
                if (!result.Verified)
                    throw new InvalidOperationException(result.Reason ?? "wrong_code");
            })
            .Build();

        var responseData = new { token, userId, name = user?.FullName ?? "", phone = MaskPhone(phone) };
        var env = await _engine.ExecuteEnvelopeAsync(op, responseData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "otp_verify_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.otp.verify", responseData);
    }

    // ─── POST /auth/logout ──────────────────────────────────────────────
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = HttpContext.Items["user_id"] as string ?? EjarSeed.CurrentUserId;

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
    private string GenerateToken(string userId, string phone)
    {
        var claims = new[]
        {
            new Claim("sub",     userId),
            new Claim("user_id", userId),
            new Claim("phone",   phone)
        };
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _jwt.Issuer, _jwt.Audience, claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string MaskPhone(string p) =>
        p.Length < 4 ? "****" : new string('*', p.Length - 4) + p[^4..];

    public sealed record OtpRequestBody(string? Phone);
    public sealed record OtpVerifyBody(string? Phone, string? Code);
}
