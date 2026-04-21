using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Ashare.V2.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Api.Controllers;

/// <summary>
/// Mock auth: OTP request → OTP verify → logout.
/// كل عملية تمرّ عبر Entry.Create وفق نموذج OAM.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly OpEngine _engine;
    public AuthController(OpEngine engine) => _engine = engine;

    // mock OTP store: phone → (otp, expiry)
    private static readonly Dictionary<string, (string Otp, DateTime Expiry)> _otpStore = new();
    private static readonly object _otpLock = new();

    // mock active sessions: token → userId
    private static readonly Dictionary<string, string> _sessions = new();
    private static readonly object _sessLock = new();

    private string CurrentUserId => HttpContext.Items["user_id"] as string ?? AshareV2Seed.CurrentUserId;
    private string Caller => $"User:{CurrentUserId}";

    // ─── POST /auth/otp/request ─────────────────────────────────────────────

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
            .Execute(ctx =>
            {
                var expiry = DateTime.UtcNow.AddSeconds(120);
                lock (_otpLock) _otpStore[phone] = ("123456", expiry);
                return Task.CompletedTask;
            })
            .Build();

        var responseData = new { maskedPhone = MaskPhone(phone), expiresInSeconds = 120 };
        var env = await _engine.ExecuteEnvelopeAsync(op, responseData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "otp_request_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.otp.request", responseData);
    }

    // ─── POST /auth/otp/verify ──────────────────────────────────────────────

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyBody body, CancellationToken ct)
    {
        var phone = body.Phone?.Trim() ?? string.Empty;
        var code  = body.Code?.Trim()  ?? string.Empty;

        // seed OTP if none exists so the mock always passes on first call
        lock (_otpLock)
        {
            if (!_otpStore.ContainsKey(phone))
                _otpStore[phone] = ("123456", DateTime.UtcNow.AddSeconds(120));
        }

        var mockToken = Guid.NewGuid().ToString("N");

        var op = Entry.Create("auth.otp.verify")
            .Describe($"OTP verification for {MaskPhone(phone)}")
            .From($"Phone:{phone}", 1, ("role", "verifier"))
            .To($"User:{AshareV2Seed.CurrentUserId}", 1, ("role", "authenticated"))
            .Tag("phone_masked", MaskPhone(phone))
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Analyze(new RequiredFieldAnalyzer("code",  () => code))
            .Analyze(new ConditionAnalyzer("code_length",
                _ => code.Length == 6 && code.All(char.IsDigit),
                "رمز التحقق يجب أن يكون 6 أرقام"))
            .Analyze(new ConditionAnalyzer("code_valid", _ =>
            {
                lock (_otpLock)
                {
                    return _otpStore.TryGetValue(phone, out var entry)
                        && DateTime.UtcNow <= entry.Expiry;
                }
            }, "رمز التحقق غير صحيح أو منتهي الصلاحية"))
            .Execute(ctx =>
            {
                lock (_otpLock) _otpStore.Remove(phone);
                lock (_sessLock) _sessions[mockToken] = AshareV2Seed.CurrentUserId;
                return Task.CompletedTask;
            })
            .Build();

        var responseData = new { token = mockToken, userId = AshareV2Seed.CurrentUserId,
                                 name = AshareV2Seed.Profile.FullName };
        var env = await _engine.ExecuteEnvelopeAsync(op, responseData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "otp_verify_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.otp.verify", responseData);
    }

    // ─── POST /auth/logout ──────────────────────────────────────────────────

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = CurrentUserId;

        var op = Entry.Create("auth.logout")
            .Describe($"User {userId} logs out")
            .From($"User:{userId}", 1, ("role", "actor"))
            .To("System:Auth", -1, ("role", "session"))
            .Tag("user_id", userId)
            .Execute(ctx =>
            {
                lock (_sessLock)
                {
                    var toRemove = _sessions
                        .Where(kv => kv.Value == userId)
                        .Select(kv => kv.Key)
                        .ToList();
                    foreach (var k in toRemove) _sessions.Remove(k);
                }
                return Task.CompletedTask;
            })
            .Build();

        var responseData = new { userId };
        var env = await _engine.ExecuteEnvelopeAsync(op, responseData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "logout_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("auth.logout", responseData);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 4) return "****";
        return new string('*', phone.Length - 4) + phone[^4..];
    }

    public sealed record OtpRequestBody(string? Phone);
    public sealed record OtpVerifyBody(string? Phone, string? Code);
}
