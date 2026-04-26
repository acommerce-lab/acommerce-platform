using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Order.V2.Domain;
using Order.V2.Vendor.Api;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Order.V2.Vendor.Api.Controllers;

[ApiController]
[Route("api/vendor/auth")]
public class AuthController : ControllerBase
{
    private readonly TwoFactorService _tfa;
    private readonly IBaseAsyncRepository<User>         _users;
    private readonly IBaseAsyncRepository<VendorEntity> _vendors;
    private readonly OpEngine _engine;
    private readonly OrderV2VendorJwtConfig _jwt;

    public AuthController(TwoFactorService tfa, IRepositoryFactory repo,
                          OpEngine engine, OrderV2VendorJwtConfig jwt)
    {
        _tfa     = tfa;
        _users   = repo.CreateRepository<User>();
        _vendors = repo.CreateRepository<VendorEntity>();
        _engine  = engine;
        _jwt     = jwt;
    }

    public record RequestOtpBody(string PhoneNumber);

    [HttpPost("sms/request")]
    public async Task<IActionResult> RequestSms([FromBody] RequestOtpBody req, CancellationToken ct)
    {
        var phone = req.PhoneNumber?.Trim() ?? "";
        var users = await _users.GetAllWithPredicateAsync(u => u.PhoneNumber == phone);
        var user  = users.FirstOrDefault(u => u.Role == "vendor");

        if (user is null)
            return this.NotFoundEnvelope("vendor_not_found", "لا يوجد حساب بائع بهذا الرقم.");

        if (!user.IsActive)
            return this.BadRequestEnvelope("vendor_suspended", "الحساب معطّل.");

        var result = await _tfa.InitiateAsync("sms", user.Id.ToString(), target: phone, ct);
        if (!result.Succeeded)
            return this.BadRequestEnvelope("otp_failed", result.Error);

        return this.OkEnvelope("auth.vendor.sms.request", new
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
        if (user.Role != "vendor") return this.UnauthorizedEnvelope("not_vendor");

        var vendorList = await _vendors.GetAllWithPredicateAsync(v => v.OwnerId == user.Id && !v.IsDeleted);
        var vendor = vendorList.FirstOrDefault();
        if (vendor is null) return this.NotFoundEnvelope("vendor_profile_not_found");

        var token = IssueJwt(user, vendor.Id);

        var op = Entry.Create("auth.vendor.verify")
            .Describe($"Vendor:{vendor.Id} verified via SMS OTP")
            .From("System:auth", 1, ("role", "auth_service"))
            .To($"User:{user.Id}", 1, ("role", "vendor"))
            .Tag("user_id", user.Id.ToString())
            .Tag("vendor_id", vendor.Id.ToString())
            .Execute(async ctx => await _users.UpdateAsync(user, ctx.CancellationToken))
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success)
            return this.BadRequestEnvelope("verify_failed", result.ErrorMessage);

        return this.OkEnvelope("auth.vendor.sms.verify", new
        {
            userId      = user.Id,
            vendorId    = vendor.Id,
            vendorName  = vendor.Name,
            phoneNumber = user.PhoneNumber,
            fullName    = user.FullName,
            role        = user.Role,
            accessToken = token
        });
    }

    private string IssueJwt(User user, Guid vendorId)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("role",                      "vendor"),
            new Claim("vendor_id",                 vendorId.ToString()),
            new Claim("name",                      user.FullName ?? ""),
            new Claim("phone",                     user.PhoneNumber ?? ""),
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
