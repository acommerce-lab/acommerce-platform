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

namespace Order.Api.Controllers;

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

    public record RequestSmsOtp(string PhoneNumber);

    [HttpPost("sms/request")]
    public async Task<IActionResult> RequestSms([FromBody] RequestSmsOtp req, CancellationToken ct)
    {
        var users = await _users.GetAllWithPredicateAsync(u => u.PhoneNumber == req.PhoneNumber);
        var user = users.FirstOrDefault();

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                PhoneNumber = req.PhoneNumber,
                Role = "customer"
            };

            var createOp = Entry.Create("auth.user_create")
                .Describe($"Auto-register user for phone {req.PhoneNumber}")
                .From("System:auth", 1, ("role", "auth_service"))
                .To($"User:{user.Id}", 1, ("role", "new_user"))
                .Tag("phone_number", req.PhoneNumber)
                .Tag("auth_method", "sms")
                .Analyze(new RequiredFieldAnalyzer("phoneNumber", () => req.PhoneNumber))
                .Execute(async ctx =>
                {
                    await _users.AddAsync(user, ctx.CancellationToken);
                    ctx.Set("userId", user.Id);
                })
                .Build();

            var createResult = await _engine.ExecuteAsync(createOp, ct);
            if (!createResult.Success)
                return this.BadRequestEnvelope("user_create_failed", createResult.ErrorMessage);
        }

        var result = await _tfa.InitiateAsync("sms", user.Id.ToString(), target: req.PhoneNumber, ct);

        if (!result.Succeeded)
            return this.BadRequestEnvelope("otp_initiate_failed", result.Error);

        return this.OkEnvelope("auth.sms.request", new
        {
            challengeId = result.ChallengeId,
            userId = user.Id,
            phoneNumber = req.PhoneNumber,
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

        var principal = new OrderPrincipal { UserId = user.Id.ToString(), DisplayName = user.FullName };
        var token = await _tokenStore.IssueAsync(principal, ct);

        // Activate user via OpEngine entry
        var verifyOp = Entry.Create("auth.user_verify")
            .Describe($"User {user.Id} verified via SMS OTP")
            .From("System:auth", 1, ("role", "auth_service"))
            .To($"User:{user.Id}", 1, ("role", "verified_user"))
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

        return this.OkEnvelope("auth.sms.verify", new
        {
            userId = user.Id,
            phoneNumber = user.PhoneNumber,
            fullName = user.FullName,
            accessToken = token.AccessToken,
            refreshToken = token.RefreshToken,
            expiresAt = token.ExpiresAt
        });
    }

    public record RequestEmailOtp(string Email);

    [HttpPost("email/request")]
    public async Task<IActionResult> RequestEmail([FromBody] RequestEmailOtp req, CancellationToken ct)
    {
        var users = await _users.GetAllWithPredicateAsync(u => u.Email == req.Email);
        var user = users.FirstOrDefault();
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                PhoneNumber = "+966-pending",
                Email = req.Email,
                Role = "customer"
            };

            var createOp = Entry.Create("auth.user_create")
                .Describe($"Auto-register user for email {req.Email}")
                .From("System:auth", 1, ("role", "auth_service"))
                .To($"User:{user.Id}", 1, ("role", "new_user"))
                .Tag("email", req.Email)
                .Tag("auth_method", "email")
                .Analyze(new RequiredFieldAnalyzer("email", () => req.Email))
                .Execute(async ctx =>
                {
                    await _users.AddAsync(user, ctx.CancellationToken);
                    ctx.Set("userId", user.Id);
                })
                .Build();

            var createResult = await _engine.ExecuteAsync(createOp, ct);
            if (!createResult.Success)
                return this.BadRequestEnvelope("user_create_failed", createResult.ErrorMessage);
        }

        var result = await _tfa.InitiateAsync("email", user.Id.ToString(), target: req.Email, ct);

        if (!result.Succeeded)
            return this.BadRequestEnvelope("otp_initiate_failed", result.Error);

        return this.OkEnvelope("auth.email.request", new { challengeId = result.ChallengeId, userId = user.Id });
    }

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
