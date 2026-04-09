# ACommerce.Authentication.JWT

## نظرة عامة | Overview

مكتبة `ACommerce.Authentication.JWT` توفر التنفيذ الكامل لمصادقة JWT (JSON Web Tokens) مع دعم توكنات الوصول والتحديث، وإدارة الجلسات، والقائمة السوداء للتوكنات الملغاة.

This library provides complete JWT (JSON Web Tokens) authentication implementation with support for access and refresh tokens, session management, and blacklisting of revoked tokens.

**المسار | Path:** `Authentication/ACommerce.Authentication.JWT`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)
**الاعتماديات | Dependencies:**
- Microsoft.AspNetCore.Authentication.JwtBearer
- System.IdentityModel.Tokens.Jwt
- ACommerce.Authentication.Abstractions
- ACommerce.SharedKernel.Abstractions

---

## المكونات الرئيسية | Core Components

### 1. JwtTokenService

خدمة إنشاء وإدارة توكنات JWT.

```csharp
public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenBlacklistService _blacklistService;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenBlacklistService blacklistService,
        ILogger<JwtTokenService> logger)
    {
        _options = options.Value;
        _refreshTokenRepository = refreshTokenRepository;
        _blacklistService = blacklistService;
        _logger = logger;
    }

    public async Task<TokenResult> GenerateAccessTokenAsync(
        IApplicationUser user,
        IEnumerable<string> roles,
        IEnumerable<Claim>? additionalClaims = null,
        CancellationToken cancellationToken = default)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        // Add phone number if present
        if (!string.IsNullOrEmpty(user.PhoneNumber))
        {
            claims.Add(new Claim("phone_number", user.PhoneNumber));
        }

        // Add roles
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Add additional claims
        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims);
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();

        return new TokenResult
        {
            Token = tokenHandler.WriteToken(token),
            ExpiresAt = expires,
            TokenType = "Bearer"
        };
    }

    public async Task<string> GenerateRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        _logger.LogInformation(
            "Generated refresh token for user {UserId}, expires at {ExpiresAt}",
            userId, refreshToken.ExpiresAt);

        return refreshToken.Token;
    }

    public async Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        // Check if token is blacklisted
        if (await _blacklistService.IsBlacklistedAsync(token, cancellationToken))
        {
            return TokenValidationResult.Invalid("Token has been revoked");
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_options.SecretKey);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = Guid.Parse(jwtToken.Subject);

            return TokenValidationResult.Valid(userId, principal.Claims);
        }
        catch (SecurityTokenExpiredException)
        {
            return TokenValidationResult.Invalid("Token has expired");
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return TokenValidationResult.Invalid("Invalid token");
        }
    }

    public async Task RevokeTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        await _blacklistService.AddToBlacklistAsync(token, cancellationToken);

        _logger.LogInformation("Token revoked and added to blacklist");
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
```

---

### 2. JwtAuthenticationService

خدمة المصادقة الكاملة مع JWT.

```csharp
public class JwtAuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITwoFactorService? _twoFactorService;
    private readonly AuthenticationOptions _options;
    private readonly ILogger<JwtAuthenticationService> _logger;

    public JwtAuthenticationService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IOptions<AuthenticationOptions> options,
        ITwoFactorService? twoFactorService = null,
        ILogger<JwtAuthenticationService> logger = null!)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _twoFactorService = twoFactorService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent email: {Email}", email);
            return AuthResult.Failure("البريد الإلكتروني أو كلمة المرور غير صحيحة");
        }

        // Check if account is locked
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            _logger.LogWarning("Login attempt for locked account: {Email}", email);
            return AuthResult.Failure("الحساب مقفل مؤقتاً. يرجى المحاولة لاحقاً");
        }

        // Verify password
        if (!_passwordHasher.VerifyPassword(user.PasswordHash, password))
        {
            await IncrementFailedLoginAttemptsAsync(user, cancellationToken);
            return AuthResult.Failure("البريد الإلكتروني أو كلمة المرور غير صحيحة");
        }

        // Check if email confirmation is required
        if (_options.RequireEmailConfirmation && !user.EmailConfirmed)
        {
            return AuthResult.Failure("يرجى تأكيد البريد الإلكتروني أولاً");
        }

        // Check if 2FA is enabled
        if (user.TwoFactorEnabled && _twoFactorService != null)
        {
            _logger.LogInformation("2FA required for user: {UserId}", user.Id);
            return AuthResult.TwoFactorRequired("SMS"); // or dynamic based on user preference
        }

        // Reset failed attempts
        await ResetFailedLoginAttemptsAsync(user, cancellationToken);

        // Generate tokens
        return await GenerateAuthResultAsync(user, cancellationToken);
    }

    public async Task<AuthResult> LoginWithPhoneAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByPhoneAsync(phoneNumber, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent phone: {Phone}", phoneNumber);
            return AuthResult.Failure("رقم الهاتف أو كلمة المرور غير صحيحة");
        }

        // Same logic as email login...
        if (!_passwordHasher.VerifyPassword(user.PasswordHash, password))
        {
            await IncrementFailedLoginAttemptsAsync(user, cancellationToken);
            return AuthResult.Failure("رقم الهاتف أو كلمة المرور غير صحيحة");
        }

        if (_options.RequirePhoneConfirmation && !user.PhoneNumberConfirmed)
        {
            return AuthResult.Failure("يرجى تأكيد رقم الهاتف أولاً");
        }

        if (user.TwoFactorEnabled && _twoFactorService != null)
        {
            return AuthResult.TwoFactorRequired("SMS");
        }

        await ResetFailedLoginAttemptsAsync(user, cancellationToken);

        return await GenerateAuthResultAsync(user, cancellationToken);
    }

    public async Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            return AuthResult.Failure("البريد الإلكتروني مستخدم مسبقاً");
        }

        // Check if phone already exists
        if (!string.IsNullOrEmpty(request.PhoneNumber) &&
            await _userRepository.ExistsByPhoneAsync(request.PhoneNumber, cancellationToken))
        {
            return AuthResult.Failure("رقم الهاتف مستخدم مسبقاً");
        }

        // Create user
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            UserName = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.AddAsync(user, cancellationToken);

        _logger.LogInformation("New user registered: {UserId} ({Email})", user.Id, user.Email);

        // Generate tokens
        return await GenerateAuthResultAsync(user, cancellationToken);
    }

    public async Task<AuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(
            refreshToken, cancellationToken);

        if (storedToken == null)
        {
            return AuthResult.Failure("Invalid refresh token");
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            await _refreshTokenRepository.DeleteAsync(storedToken, cancellationToken);
            return AuthResult.Failure("Refresh token has expired");
        }

        if (storedToken.IsRevoked)
        {
            _logger.LogWarning(
                "Attempt to use revoked refresh token for user: {UserId}",
                storedToken.UserId);
            return AuthResult.Failure("Refresh token has been revoked");
        }

        var user = await _userRepository.GetByIdAsync(storedToken.UserId, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return AuthResult.Failure("User not found or inactive");
        }

        // Revoke old refresh token
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);

        // Generate new tokens
        return await GenerateAuthResultAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        // This would be called with the current user's context
        // Typically revokes the current refresh token
    }

    public async Task RevokeAllSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _refreshTokenRepository.GetActiveByUserIdAsync(userId, cancellationToken);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _refreshTokenRepository.UpdateRangeAsync(tokens, cancellationToken);

        _logger.LogInformation("All sessions revoked for user: {UserId}", userId);
    }

    private async Task<AuthResult> GenerateAuthResultAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var roles = await _userRepository.GetRolesAsync(user.Id, cancellationToken);

        var accessToken = await _tokenService.GenerateAccessTokenAsync(
            user, roles, null, cancellationToken);

        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(
            user.Id, cancellationToken);

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        return AuthResult.Success(
            accessToken.Token,
            refreshToken,
            accessToken.ExpiresAt,
            DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays),
            new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                UserName = user.UserName,
                FullName = $"{user.Profile?.FirstName} {user.Profile?.LastName}".Trim(),
                AvatarUrl = user.Profile?.AvatarUrl,
                Roles = roles.ToList()
            });
    }

    private async Task IncrementFailedLoginAttemptsAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        user.FailedLoginAttempts++;

        if (user.FailedLoginAttempts >= _options.LockoutThreshold)
        {
            user.LockoutEnd = DateTime.UtcNow.AddMinutes(_options.LockoutDurationMinutes);
            _logger.LogWarning(
                "Account locked due to failed attempts: {UserId} ({Email})",
                user.Id, user.Email);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    private async Task ResetFailedLoginAttemptsAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (user.FailedLoginAttempts > 0)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _userRepository.UpdateAsync(user, cancellationToken);
        }
    }
}
```

---

### 3. خيارات JWT | JWT Options

```csharp
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// المفتاح السري لتوقيع التوكنات
    /// يجب أن يكون طويلاً (256+ بت) وآمناً
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// مُصدر التوكن
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// الجمهور المستهدف
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// مدة صلاحية توكن الوصول بالدقائق
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// مدة صلاحية توكن التحديث بالأيام
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// خوارزمية التوقيع
    /// </summary>
    public string Algorithm { get; set; } = SecurityAlgorithms.HmacSha256;
}
```

---

### 4. Token Blacklist Service

خدمة القائمة السوداء للتوكنات الملغاة.

```csharp
public interface ITokenBlacklistService
{
    Task AddToBlacklistAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> IsBlacklistedAsync(string token, CancellationToken cancellationToken = default);
}

// In-Memory Implementation (للتطوير)
public class InMemoryTokenBlacklistService : ITokenBlacklistService
{
    private readonly HashSet<string> _blacklist = new();
    private readonly object _lock = new();

    public Task AddToBlacklistAsync(string token, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _blacklist.Add(token);
        }
        return Task.CompletedTask;
    }

    public Task<bool> IsBlacklistedAsync(string token, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_blacklist.Contains(token));
        }
    }
}

// Redis Implementation (للإنتاج)
public class RedisTokenBlacklistService : ITokenBlacklistService
{
    private readonly IDistributedCache _cache;
    private readonly JwtOptions _jwtOptions;

    public RedisTokenBlacklistService(
        IDistributedCache cache,
        IOptions<JwtOptions> jwtOptions)
    {
        _cache = cache;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task AddToBlacklistAsync(string token, CancellationToken cancellationToken = default)
    {
        var key = $"blacklist:{GetTokenHash(token)}";
        var expiration = TimeSpan.FromMinutes(_jwtOptions.AccessTokenExpirationMinutes + 5);

        await _cache.SetStringAsync(key, "revoked", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        }, cancellationToken);
    }

    public async Task<bool> IsBlacklistedAsync(string token, CancellationToken cancellationToken = default)
    {
        var key = $"blacklist:{GetTokenHash(token)}";
        var value = await _cache.GetStringAsync(key, cancellationToken);
        return value != null;
    }

    private static string GetTokenHash(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
```

---

### 5. Password Hasher

خدمة تشفير كلمات المرور.

```csharp
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string hash, string password);
}

public class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 4;
    private const int MemorySize = 65536; // 64 MB
    private const int Parallelism = 4;

    public string HashPassword(string password)
    {
        var salt = GenerateSalt();
        var hash = Argon2.Hash(
            password,
            salt,
            HashSize,
            Iterations,
            MemorySize,
            Parallelism,
            Argon2Type.Argon2id);

        return $"{Convert.ToBase64String(salt)}:{hash}";
    }

    public bool VerifyPassword(string storedHash, string password)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var hash = parts[1];

        var computedHash = Argon2.Hash(
            password,
            salt,
            HashSize,
            Iterations,
            MemorySize,
            Parallelism,
            Argon2Type.Argon2id);

        return hash == computedHash;
    }

    private static byte[] GenerateSalt()
    {
        var salt = new byte[SaltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }
}

// Alternative: BCrypt Implementation
public class BCryptPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }

    public bool VerifyPassword(string hash, string password)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
```

---

## تسجيل الخدمات | Service Registration

```csharp
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure JWT options
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

        // Add JWT Bearer authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions!.SecretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            // SignalR support
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        // Register services
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthenticationService, JwtAuthenticationService>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        // Register blacklist service
        services.AddSingleton<ITokenBlacklistService, InMemoryTokenBlacklistService>();
        // For production: services.AddScoped<ITokenBlacklistService, RedisTokenBlacklistService>();

        return services;
    }

    public static IServiceCollection AddJwtAuthenticationWithRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddJwtAuthentication(configuration);

        // Replace with Redis implementation
        services.AddScoped<ITokenBlacklistService, RedisTokenBlacklistService>();

        return services;
    }
}
```

---

## تكوين appsettings.json | Configuration

```json
{
  "Jwt": {
    "SecretKey": "your-256-bit-secret-key-here-at-least-32-characters",
    "Issuer": "https://your-api.com",
    "Audience": "https://your-app.com",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Authentication": {
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7,
    "AllowMultipleSessions": true,
    "MaxActiveSessions": 5,
    "LockoutThreshold": 5,
    "LockoutDurationMinutes": 15,
    "RequireEmailConfirmation": true,
    "RequirePhoneConfirmation": false,
    "Password": {
      "MinimumLength": 8,
      "MaximumLength": 128,
      "RequireDigit": true,
      "RequireLowercase": true,
      "RequireUppercase": true,
      "RequireNonAlphanumeric": true,
      "RequiredUniqueChars": 4
    }
  }
}
```

---

## أفضل الممارسات الأمنية | Security Best Practices

### 1. تخزين المفتاح السري
```csharp
// ❌ لا تفعل هذا أبداً
public const string SecretKey = "my-secret-key";

// ✅ استخدم متغيرات البيئة أو Azure Key Vault
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
// أو
var secretKey = await keyVault.GetSecretAsync("jwt-secret-key");
```

### 2. مدة صلاحية قصيرة للتوكنات
```json
{
  "AccessTokenExpirationMinutes": 15,  // ✅ قصيرة
  "RefreshTokenExpirationDays": 7      // ✅ معقولة
}
```

### 3. استخدام HTTPS فقط
```csharp
app.UseHttpsRedirection();
```

### 4. تخزين Refresh Token بأمان
```csharp
// في قاعدة البيانات مع تشفير
public class RefreshToken
{
    public string TokenHash { get; set; } // Store hash, not plain text
}
```

---

## المراجع | References

- [JWT.io](https://jwt.io/)
- [RFC 7519 - JSON Web Token](https://tools.ietf.org/html/rfc7519)
- [OWASP JWT Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html)
- [ASP.NET Core JWT Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/jwt-auth)
