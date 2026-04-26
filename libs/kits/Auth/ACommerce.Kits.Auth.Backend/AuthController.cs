using ACommerce.Authentication.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
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
/// SMS OTP auth controller — drop-in. Apps register an <see cref="IAuthUserStore"/>
/// + an <see cref="AuthKitJwtConfig"/> via <see cref="AuthKitExtensions.AddAuthKit"/>;
/// the kit handles request/verify/logout, JWT issuance, OAM operation logging,
/// and analyzer-based input validation.
///
/// <para>التطبيق لا يكتب AuthController بعد الآن — هذه نسخة واحدة لكلّ الأدوار.
/// الفرق بين Customer/Provider/Admin يكون عبر <c>JwtConfig.Role</c> +
/// <c>PartyKind</c>.</para>
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly ITwoFactorChannel _otpChannel;
    private readonly IAuthUserStore _users;
    private readonly AuthKitJwtConfig _jwt;

    public AuthController(
        OpEngine engine, ITwoFactorChannel otpChannel,
        IAuthUserStore users, AuthKitJwtConfig jwt)
    {
        _engine = engine; _otpChannel = otpChannel; _users = users; _jwt = jwt;
    }

    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequestBody body, CancellationToken ct)
    {
        var phone = PhoneNormalization.Normalize(body.Phone);
        var op = Entry.Create("auth.otp.request")
            .Describe($"OTP requested for {MaskPhone(phone)}")
            .From("System:Auth", 1, ("role", "issuer"))
            .To($"Phone:{phone}", 1, ("role", "recipient"))
            .Tag("phone_masked", MaskPhone(phone))
            .Tag("role", _jwt.Role)
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

        var userId      = await _users.GetOrCreateUserIdAsync(phone, ct);
        var displayName = await _users.GetDisplayNameAsync(userId, ct) ?? "";
        var token       = GenerateToken(userId, phone);

        var op = Entry.Create("auth.otp.verify")
            .Describe($"OTP verify for {MaskPhone(phone)}")
            .From($"Phone:{phone}", 1, ("role", "verifier"))
            .To($"{_jwt.PartyKind}:{userId}", 1, ("role", "authenticated"))
            .Tag("user_id", userId).Tag("role", _jwt.Role)
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

        var data = new { token, userId, name = displayName, phone = MaskPhone(phone), role = _jwt.Role };
        var env = await _engine.ExecuteEnvelopeAsync(op, data, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "otp_verify_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.otp.verify", data);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var userId = User.FindFirst("user_id")?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? "";
        return this.OkEnvelope("auth.logout", new { userId });
    }

    private string GenerateToken(string userId, string phone)
    {
        var claims = new[]
        {
            new Claim("sub",     userId),
            new Claim("user_id", userId),
            new Claim("phone",   phone),
            new Claim("role",    _jwt.Role),
        };
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims,
            expires: DateTime.UtcNow.AddDays(_jwt.AccessTokenLifetimeDays),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string MaskPhone(string p) =>
        p.Length < 4 ? "****" : new string('*', p.Length - 4) + p[^4..];

    public sealed record OtpRequestBody(string? Phone);
    public sealed record OtpVerifyBody(string? Phone, string? Code);
}
