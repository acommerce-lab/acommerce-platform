using ACommerce.Authentication.Operations;
using ACommerce.Authentication.Operations.Abstractions;
using ACommerce.Authentication.Providers.Token;
using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Order.Api.Entities;
using Order.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.Admin.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly TwoFactorService _tfa;
    private readonly JwtTokenStore _tokenStore;
    private readonly IBaseAsyncRepository<User> _users;
    private readonly OpEngine _engine;

    public AuthController(
        AuthService auth,
        TwoFactorService tfa,
        JwtTokenStore tokenStore,
        IRepositoryFactory repoFactory,
        OpEngine engine)
    {
        _auth = auth;
        _tfa = tfa;
        _tokenStore = tokenStore;
        _users = repoFactory.CreateRepository<User>();
        _engine = engine;
    }

    // ─────────────────────────────────────────────────────────
    // 2FA: SMS - Initiate (تسجيل دخول المسؤول بالهاتف)
    // ─────────────────────────────────────────────────────────

    public record RequestSmsOtp(string PhoneNumber);

    [HttpPost("sms/request")]
    public async Task<IActionResult> RequestSms([FromBody] RequestSmsOtp req, CancellationToken ct)
    {
        var phone = PhoneNormalization.Normalize(req.PhoneNumber);
        var users = await _users.GetAllWithPredicateAsync(u => u.PhoneNumber == phone);
        var user = users.FirstOrDefault();

        if (user == null)
            return this.NotFoundEnvelope("admin_not_found",
                "لا يوجد حساب مسؤول بهذا الرقم. تواصل مع الإدارة.");

        if (user.Role != "admin")
            return this.UnauthorizedEnvelope("not_admin",
                "هذا الحساب ليس حساب مسؤول");

        if (!user.IsActive)
            return this.BadRequestEnvelope("admin_suspended",
                "حساب المسؤول معطّل");

        var result = await _tfa.InitiateAsync("sms", user.Id.ToString(), target: phone, ct);

        if (!result.Succeeded)
            return this.BadRequestEnvelope("otp_initiate_failed", result.Error);

        return this.OkEnvelope("auth.admin.sms.request", new
        {
            challengeId = result.ChallengeId,
            userId = user.Id,
            phoneNumber = phone,
            message = "تم إرسال الكود (راجع الـ logs في الوضع التجريبي)"
        });
    }

    public record VerifySmsOtp(Guid UserId, string ChallengeId, string Code);

    [HttpPost("sms/verify")]
    public async Task<IActionResult> VerifySms([FromBody] VerifySmsOtp req, CancellationToken ct)
    {
        var verification = await _tfa.VerifyAsync(
            "sms", req.UserId.ToString(), req.ChallengeId, req.Code, ct);

        if (!verification.Verified)
            return this.UnauthorizedEnvelope("otp_invalid", verification.Reason);

        var user = await _users.GetByIdAsync(req.UserId, ct);
        if (user == null) return this.NotFoundEnvelope("user_not_found");

        if (user.Role != "admin")
            return this.UnauthorizedEnvelope("not_admin", "هذا الحساب ليس حساب مسؤول");

        // Issue JWT with admin role claim. Without this, the AdminOnly
        // policy (RequireClaim("role", "admin")) rejects every request.
        var principal = new OrderPrincipal
        {
            UserId = user.Id.ToString(),
            DisplayName = user.FullName,
            Claims = new Dictionary<string, string>
            {
                ["role"] = user.Role ?? "admin"
            }
        };
        var token = await _tokenStore.IssueAsync(principal, ct);

        var verifyOp = Entry.Create("auth.admin.verify")
            .Describe($"Admin {user.Id} verified via SMS OTP")
            .From("System:auth", 1, ("role", "auth_service"))
            .To($"User:{user.Id}", 1, ("role", "admin"))
            .Tag("user_id", user.Id.ToString())
            .Tag("verification_method", "sms")
            .Tag("phone_number", user.PhoneNumber)
            .Execute(async ctx =>
            {
                user.IsActive = true;
                await _users.UpdateAsync(user, ctx.CancellationToken);
                ctx.Set("userId", user.Id);
                ctx.Set("accessToken", token.AccessToken);
            })
            .Build();

        var verifyResult = await _engine.ExecuteAsync(verifyOp, ct);
        if (!verifyResult.Success)
            return this.BadRequestEnvelope("verify_failed", verifyResult.ErrorMessage);

        return this.OkEnvelope("auth.admin.sms.verify", new
        {
            userId = user.Id,
            phoneNumber = user.PhoneNumber,
            fullName = user.FullName,
            role = user.Role,
            accessToken = token.AccessToken,
            refreshToken = token.RefreshToken,
            expiresAt = token.ExpiresAt
        });
    }

    // ─────────────────────────────────────────────────────────
    // Token validation
    // ─────────────────────────────────────────────────────────

    public record TokenLoginRequest(string AccessToken);

    [HttpPost("token/validate")]
    public async Task<IActionResult> ValidateToken([FromBody] TokenLoginRequest req, CancellationToken ct)
    {
        var result = await _auth.ValidateAsync("token", new TokenCredential(req.AccessToken), ct);
        if (!result.Succeeded)
            return this.UnauthorizedEnvelope("invalid_token", result.Reason);

        return this.OkEnvelope("auth.token.validate", new
        {
            valid = true,
            userId = result.Principal?.UserId,
            displayName = result.Principal?.DisplayName
        });
    }

    public record RefreshRequest(string UserId, string RefreshToken);

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(req.UserId, req.RefreshToken, ct);
        if (!result.Succeeded)
            return this.UnauthorizedEnvelope("refresh_failed", result.Reason);

        return this.OkEnvelope("auth.refresh", new
        {
            accessToken = result.Token!.AccessToken,
            refreshToken = result.Token!.RefreshToken,
            expiresAt = result.Token!.ExpiresAt
        });
    }

    [HttpPost("signout")]
    public async Task<IActionResult> SignOut([FromBody] TokenLoginRequest req, CancellationToken ct)
    {
        var validation = await _auth.ValidateAsync("token", new TokenCredential(req.AccessToken), ct);
        if (!validation.Succeeded || validation.Principal == null)
            return this.UnauthorizedEnvelope();

        await _auth.SignOutAsync(validation.Principal.UserId, req.AccessToken, ct);
        return this.OkEnvelope("auth.signout", new { signedOut = true });
    }
}
