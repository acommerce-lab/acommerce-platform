using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.V2.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Ashare.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly TwoFactorService _tfa;
    private readonly AdminV2JwtConfig _jwt;
    private readonly IBaseAsyncRepository<Profile> _profiles;
    private readonly OpEngine _engine;

    public AuthController(TwoFactorService tfa, AdminV2JwtConfig jwt,
                          IRepositoryFactory factory, OpEngine engine)
    {
        _tfa      = tfa;
        _jwt      = jwt;
        _profiles = factory.CreateRepository<Profile>();
        _engine   = engine;
    }

    public record RequestOtpDto(string PhoneNumber);

    [HttpPost("sms/request")]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpDto req, CancellationToken ct)
    {
        var phone = (req.PhoneNumber ?? "").Trim();
        var list  = await _profiles.GetAllWithPredicateAsync(p => p.PhoneNumber == phone);
        var profile = list.FirstOrDefault();

        if (profile == null)
            return this.NotFoundEnvelope("admin_not_found", "لا يوجد حساب مسؤول بهذا الرقم");
        if (profile.Role != "admin")
            return this.UnauthorizedEnvelope("not_admin", "هذا الحساب ليس حساب مسؤول");
        if (!profile.IsActive)
            return this.BadRequestEnvelope("admin_suspended", "حساب المسؤول معطّل");

        var result = await _tfa.InitiateAsync("sms", profile.Id.ToString(), target: phone, ct);
        if (!result.Succeeded)
            return this.BadRequestEnvelope("otp_failed", result.Error);

        return this.OkEnvelope("auth.admin.sms.request", new
        {
            challengeId = result.ChallengeId,
            userId      = profile.Id,
            message     = "تم إرسال الرمز (راجع السجلات في وضع التطوير)"
        });
    }

    public record VerifyOtpDto(Guid UserId, string ChallengeId, string Code);

    [HttpPost("sms/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto req, CancellationToken ct)
    {
        var verification = await _tfa.VerifyAsync("sms", req.UserId.ToString(),
                                                   req.ChallengeId, req.Code, ct);
        if (!verification.Verified)
            return this.UnauthorizedEnvelope("otp_invalid", verification.Reason);

        var profile = await _profiles.GetByIdAsync(req.UserId, ct);
        if (profile == null) return this.NotFoundEnvelope("user_not_found");
        if (profile.Role != "admin")
            return this.UnauthorizedEnvelope("not_admin", "هذا الحساب ليس حساب مسؤول");

        string? accessToken = null;

        var op = Entry.Create("auth.admin.sms.verify")
            .Describe($"Admin {profile.Id} verified via SMS OTP")
            .From("System:auth", 1, ("role", "auth_service"))
            .To($"User:{profile.Id}", 1, ("role", "admin"))
            .Tag("user_id", profile.Id.ToString())
            .Execute(async ctx =>
            {
                accessToken = IssueJwt(profile);
                await Task.CompletedTask;
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success)
            return this.BadRequestEnvelope("verify_failed", result.ErrorMessage);

        return this.OkEnvelope("auth.admin.sms.verify", new
        {
            userId      = profile.Id,
            fullName    = profile.FullName,
            phoneNumber = profile.PhoneNumber,
            role        = profile.Role,
            accessToken,
            expiresAt   = DateTime.UtcNow.AddDays(30)
        });
    }

    private string IssueJwt(Profile profile)
    {
        var key  = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub",         profile.Id.ToString()),
            new Claim("role",        profile.Role ?? "admin"),
            new Claim("name",        profile.FullName ?? ""),
            new Claim("phone",       profile.PhoneNumber ?? ""),
        };
        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
