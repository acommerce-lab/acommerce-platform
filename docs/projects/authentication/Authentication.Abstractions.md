# ACommerce.Authentication.Abstractions

## نظرة عامة | Overview

مكتبة `ACommerce.Authentication.Abstractions` توفر الواجهات والعقود المجردة لنظام المصادقة بالكامل. تعمل كطبقة تجريد تسمح بتبديل مزودي المصادقة دون تغيير كود التطبيق.

This library provides abstract interfaces and contracts for the entire authentication system. It serves as an abstraction layer that allows swapping authentication providers without changing application code.

**المسار | Path:** `Authentication/ACommerce.Authentication.Abstractions`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)
**الاعتماديات | Dependencies:**
- ACommerce.SharedKernel.Abstractions

---

## المكونات الرئيسية | Core Components

### 1. واجهات المستخدم | User Interfaces

#### IApplicationUser
الواجهة الأساسية لمستخدم التطبيق.

```csharp
public interface IApplicationUser
{
    Guid Id { get; }
    string Email { get; }
    string? PhoneNumber { get; }
    string? UserName { get; }
    bool EmailConfirmed { get; }
    bool PhoneNumberConfirmed { get; }
    bool TwoFactorEnabled { get; }
    bool IsActive { get; }
    DateTime CreatedAt { get; }
    DateTime? LastLoginAt { get; }
}
```

#### IUserProfile
ملف المستخدم الشخصي.

```csharp
public interface IUserProfile
{
    Guid UserId { get; }
    string? FirstName { get; }
    string? LastName { get; }
    string? FullName { get; }
    string? AvatarUrl { get; }
    string? PreferredLanguage { get; }
    string? TimeZone { get; }
}
```

#### IUserRole
أدوار المستخدم.

```csharp
public interface IUserRole
{
    Guid Id { get; }
    string Name { get; }
    string? Description { get; }
    IReadOnlyList<string> Permissions { get; }
}
```

---

### 2. خدمات المصادقة | Authentication Services

#### IAuthenticationService
خدمة المصادقة الرئيسية.

```csharp
public interface IAuthenticationService
{
    /// <summary>
    /// تسجيل الدخول بالبريد الإلكتروني وكلمة المرور
    /// </summary>
    Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// تسجيل الدخول برقم الهاتف وكلمة المرور
    /// </summary>
    Task<AuthResult> LoginWithPhoneAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// تسجيل مستخدم جديد
    /// </summary>
    Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// تسجيل الخروج
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// تحديث التوكن
    /// </summary>
    Task<AuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// إلغاء جميع الجلسات للمستخدم
    /// </summary>
    Task RevokeAllSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
```

#### IPasswordService
خدمة إدارة كلمات المرور.

```csharp
public interface IPasswordService
{
    /// <summary>
    /// طلب إعادة تعيين كلمة المرور
    /// </summary>
    Task<Result> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// إعادة تعيين كلمة المرور
    /// </summary>
    Task<Result> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// تغيير كلمة المرور للمستخدم الحالي
    /// </summary>
    Task<Result> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// التحقق من قوة كلمة المرور
    /// </summary>
    PasswordStrength ValidatePasswordStrength(string password);
}
```

#### ITokenService
خدمة إدارة التوكنات.

```csharp
public interface ITokenService
{
    /// <summary>
    /// إنشاء توكن وصول جديد
    /// </summary>
    Task<TokenResult> GenerateAccessTokenAsync(
        IApplicationUser user,
        IEnumerable<string> roles,
        IEnumerable<Claim>? additionalClaims = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// إنشاء توكن تحديث جديد
    /// </summary>
    Task<string> GenerateRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// التحقق من صحة التوكن
    /// </summary>
    Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// إلغاء توكن
    /// </summary>
    Task RevokeTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
}
```

---

### 3. التحقق بخطوتين | Two-Factor Authentication

#### ITwoFactorService
واجهة موحدة لجميع مزودي التحقق بخطوتين.

```csharp
public interface ITwoFactorService
{
    /// <summary>
    /// اسم المزود (SMS, Email, TOTP, Nafath, etc.)
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// إرسال رمز التحقق
    /// </summary>
    Task<Result> SendCodeAsync(
        Guid userId,
        string destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// التحقق من الرمز
    /// </summary>
    Task<Result<TwoFactorVerificationResult>> VerifyCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// تفعيل التحقق بخطوتين للمستخدم
    /// </summary>
    Task<Result> EnableAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// إلغاء تفعيل التحقق بخطوتين
    /// </summary>
    Task<Result> DisableAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
```

#### ITwoFactorProvider
واجهة لتسجيل مزودي التحقق المختلفة.

```csharp
public interface ITwoFactorProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<ITwoFactorService> GetServiceAsync();
}
```

---

### 4. نماذج البيانات | Data Models

#### AuthResult
نتيجة عملية المصادقة.

```csharp
public class AuthResult
{
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public IReadOnlyList<string>? Errors { get; set; }

    // التوكنات - تعبأ عند النجاح
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAt { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    // معلومات المستخدم
    public UserInfo? User { get; set; }

    // هل يتطلب التحقق بخطوتين؟
    public bool RequiresTwoFactor { get; set; }
    public string? TwoFactorProvider { get; set; }

    // Factory methods
    public static AuthResult Success(
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiry,
        DateTime refreshTokenExpiry,
        UserInfo user) => new()
    {
        Succeeded = true,
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        AccessTokenExpiresAt = accessTokenExpiry,
        RefreshTokenExpiresAt = refreshTokenExpiry,
        User = user
    };

    public static AuthResult Failure(string error) => new()
    {
        Succeeded = false,
        Error = error
    };

    public static AuthResult TwoFactorRequired(string provider) => new()
    {
        Succeeded = false,
        RequiresTwoFactor = true,
        TwoFactorProvider = provider
    };
}
```

#### RegisterRequest
طلب تسجيل مستخدم جديد.

```csharp
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    // معلومات الملف الشخصي
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    // معلومات المستأجر (للتسجيل متعدد المستأجرين)
    public Guid? TenantId { get; set; }

    // أدوار افتراضية
    public IList<string>? Roles { get; set; }
}
```

#### UserInfo
معلومات المستخدم المرسلة مع الاستجابة.

```csharp
public class UserInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string>? Claims { get; set; }
}
```

#### TokenResult
نتيجة إنشاء التوكن.

```csharp
public class TokenResult
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string TokenType { get; set; } = "Bearer";
}
```

#### PasswordStrength
قوة كلمة المرور.

```csharp
public enum PasswordStrength
{
    VeryWeak = 0,
    Weak = 1,
    Fair = 2,
    Strong = 3,
    VeryStrong = 4
}

public class PasswordValidationResult
{
    public PasswordStrength Strength { get; set; }
    public bool IsValid { get; set; }
    public IReadOnlyList<string> Feedback { get; set; } = Array.Empty<string>();
    public int Score { get; set; }
}
```

---

### 5. الأحداث | Events

#### UserRegisteredEvent
حدث تسجيل مستخدم جديد.

```csharp
public record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string? PhoneNumber,
    DateTime RegisteredAt
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

#### UserLoggedInEvent
حدث تسجيل الدخول.

```csharp
public record UserLoggedInEvent(
    Guid UserId,
    string Email,
    string IpAddress,
    string? UserAgent,
    DateTime LoggedInAt
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

#### PasswordResetRequestedEvent
حدث طلب إعادة تعيين كلمة المرور.

```csharp
public record PasswordResetRequestedEvent(
    Guid UserId,
    string Email,
    string ResetToken,
    DateTime ExpiresAt
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

---

### 6. خيارات التكوين | Configuration Options

#### AuthenticationOptions
خيارات المصادقة العامة.

```csharp
public class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    /// <summary>
    /// مدة صلاحية توكن الوصول (بالدقائق)
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// مدة صلاحية توكن التحديث (بالأيام)
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// السماح بجلسات متعددة
    /// </summary>
    public bool AllowMultipleSessions { get; set; } = true;

    /// <summary>
    /// أقصى عدد جلسات نشطة
    /// </summary>
    public int MaxActiveSessions { get; set; } = 5;

    /// <summary>
    /// قفل الحساب بعد محاولات فاشلة
    /// </summary>
    public int LockoutThreshold { get; set; } = 5;

    /// <summary>
    /// مدة قفل الحساب (بالدقائق)
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>
    /// التحقق من البريد الإلكتروني مطلوب
    /// </summary>
    public bool RequireEmailConfirmation { get; set; } = true;

    /// <summary>
    /// التحقق من رقم الهاتف مطلوب
    /// </summary>
    public bool RequirePhoneConfirmation { get; set; } = false;
}
```

#### PasswordOptions
خيارات كلمة المرور.

```csharp
public class PasswordOptions
{
    public const string SectionName = "Authentication:Password";

    public int MinimumLength { get; set; } = 8;
    public int MaximumLength { get; set; } = 128;
    public bool RequireDigit { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = true;
    public int RequiredUniqueChars { get; set; } = 4;
}
```

---

## التكامل | Integration

### تسجيل الخدمات | Service Registration

```csharp
public static class AuthenticationAbstractionsExtensions
{
    public static IServiceCollection AddAuthenticationAbstractions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<AuthenticationOptions>(
            configuration.GetSection(AuthenticationOptions.SectionName));

        services.Configure<PasswordOptions>(
            configuration.GetSection(PasswordOptions.SectionName));

        return services;
    }
}
```

### استخدام في Controller | Usage in Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IPasswordService _passwordService;
    private readonly ITwoFactorService _twoFactorService;

    public AuthController(
        IAuthenticationService authService,
        IPasswordService passwordService,
        ITwoFactorService twoFactorService)
    {
        _authService = authService;
        _passwordService = passwordService;
        _twoFactorService = twoFactorService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResult>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        if (!result.Succeeded)
        {
            if (result.RequiresTwoFactor)
                return Ok(result); // Client should redirect to 2FA

            return Unauthorized(result.Error);
        }

        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResult>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return CreatedAtAction(nameof(Login), result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResult>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);

        if (!result.Succeeded)
            return Unauthorized(result.Error);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return NoContent();
    }

    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        // Always return success to prevent email enumeration
        await _passwordService.RequestPasswordResetAsync(request.Email);
        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _passwordService.ResetPasswordAsync(
            request.Email,
            request.Token,
            request.NewPassword);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return Ok(new { message = "Password reset successfully." });
    }

    [Authorize]
    [HttpPost("2fa/send")]
    public async Task<IActionResult> SendTwoFactorCode([FromBody] Send2FARequest request)
    {
        var userId = User.GetUserId();
        var result = await _twoFactorService.SendCodeAsync(userId, request.Destination);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return Ok(new { message = "Code sent successfully." });
    }

    [HttpPost("2fa/verify")]
    public async Task<ActionResult<AuthResult>> VerifyTwoFactorCode([FromBody] Verify2FARequest request)
    {
        var result = await _twoFactorService.VerifyCodeAsync(request.UserId, request.Code);

        if (result.IsFailure)
            return Unauthorized(result.Error);

        // Generate tokens after successful 2FA
        // ... generate and return tokens
    }
}
```

---

## مخطط العلاقات | Relationships Diagram

```
┌────────────────────────────────────────────────────────────────────┐
│                   ACommerce.Authentication                          │
└────────────────────────────────────────────────────────────────────┘
                                  │
         ┌────────────────────────┼────────────────────────┐
         │                        │                        │
         ↓                        ↓                        ↓
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Abstractions   │    │      JWT        │    │   TwoFactor     │
│                 │    │                 │    │                 │
│ - Interfaces    │←───│ - JwtService    │    │ ┌─────────────┐ │
│ - Contracts     │    │ - TokenHandler  │    │ │    SMS      │ │
│ - Events        │    │ - Validation    │    │ ├─────────────┤ │
│ - Options       │    │                 │    │ │   Email     │ │
└─────────────────┘    └─────────────────┘    │ ├─────────────┤ │
         ↑                        ↑            │ │   Nafath    │ │
         │                        │            │ └─────────────┘ │
         │                        │            └─────────────────┘
         │                        │                    ↑
         └────────────────────────┴────────────────────┘
```

---

## المراجع | References

- [OAuth 2.0 Specification](https://oauth.net/2/)
- [JWT.io](https://jwt.io/)
- [OWASP Authentication Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [ASP.NET Core Identity](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity)
