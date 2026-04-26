using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Order.V2.Admin.Api;
using Order.V2.Domain;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Order.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/auth")]
public class AuthController : ControllerBase
{
    private readonly TwoFactorService _tfa;
    private readonly IBaseAsyncRepository<User> _users;
    private readonly OpEngine _engine;
    private readonly OrderV2AdminJwtConfig _jwt;

    public AuthController(TwoFactorService tfa, IRepositoryFactory repo,
                          OpEngine engine, OrderV2AdminJwtConfig jwt)
    {
        _tfa    = tfa;
        _users  = repo.CreateRepository<User>();
        _engine = engine;
        _jwt    = jwt;
    }

    public record RequestOtpBody(string PhoneNumber);

    [HttpPost("sms/request")]
    public async Task<IActionResult> RequestSms([FromBody] RequestOtpBody req, CancellationToken ct)
    {
        var phone = req.PhoneNumber?.Trim() ?? "";
        var users = await _users.GetAllWithPredicateAsync(u => u.PhoneNumber == phone);
        var user  = users.FirstOrDefault(u => u.Role == "admin");

        if (user is null)
            return this.NotFoundEnvelope("admin_not_found", "لا يوجد حساب مسؤول بهذا الرقم.");

        if (!user.IsActive)
            return this.BadRequestEnvelope("admin_suspended", "الحساب معطّل.");

        var result = await _tfa.InitiateAsync("sms", user.Id.ToString(), target: phone, ct);
        if (!result.Succeeded)
            return this.BadRequestEnvelope("otp_failed", result.Error);

        return this.OkEnvelope("auth.admin.sms.request", new
        {
            challengeId = result.ChallengeId,
            userId      = user.Id,
            message     = "تم الإرسال (راجع الـ logs في البيئة التجريبية)"
        });
    }

    public record VerifyOtpBody(Guid UserId, string ChallengeId, string Code);

    [HttpPost("sms/verify")]
    public async Task<IActionResult> VerifySms([FromBody] VerifyOtpBody req, CancellationToken ct)
    {
        var verification = await _tfa.VerifyAsync(
            "sms", req.UserId.ToString(), req.ChallengeId, req.Code, ct);

        if (!verification.Verified)
            return this.UnauthorizedEnvelope("otp_invalid", verification.Reason);

        var user = await _users.GetByIdAsync(req.UserId, ct);
        if (user is null) return this.NotFoundEnvelope("user_not_found");
        if (user.Role != "admin") return this.UnauthorizedEnvelope("not_admin");

        var token = IssueJwt(user);

        var op = Entry.Create("auth.admin.verify")
            .Describe($"Admin:{user.Id} verified via SMS OTP")
            .From("System:auth", 1, ("role", "auth_service"))
            .To($"User:{user.Id}", 1, ("role", "admin"))
            .Tag("user_id", user.Id.ToString())
            .Execute(async ctx => await _users.UpdateAsync(user, ctx.CancellationToken))
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success)
            return this.BadRequestEnvelope("verify_failed", result.ErrorMessage);

        return this.OkEnvelope("auth.admin.sms.verify", new
        {
            userId      = user.Id,
            phoneNumber = user.PhoneNumber,
            fullName    = user.FullName,
            role        = user.Role,
            accessToken = token
        });
    }

    private string IssueJwt(User user)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,  user.Id.ToString()),
            new Claim("role",                       user.Role ?? "admin"),
            new Claim("name",                       user.FullName ?? ""),
            new Claim("phone",                      user.PhoneNumber),
        };
        var token = new JwtSecurityToken(
            issuer:   _jwt.Issuer,
            audience: _jwt.Audience,
            claims:   claims,
            expires:  DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
