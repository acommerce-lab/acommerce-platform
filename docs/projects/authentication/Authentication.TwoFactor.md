# ACommerce.Authentication.TwoFactor

## نظرة عامة | Overview

مكتبات `ACommerce.Authentication.TwoFactor.*` توفر نظام تحقق بخطوتين (2FA) متكامل وقابل للتوسع. يدعم النظام مزودين متعددين يمكن تفعيلهم واستخدامهم بشكل منفصل أو مجتمعين.

The `ACommerce.Authentication.TwoFactor.*` libraries provide a complete and extensible Two-Factor Authentication (2FA) system. The system supports multiple providers that can be enabled and used separately or in combination.

---

## المزودون المتاحون | Available Providers

| المزود | المكتبة | الوصف |
|--------|---------|-------|
| SMS | `ACommerce.Authentication.TwoFactor.SMS` | إرسال رموز OTP عبر الرسائل النصية |
| Email | `ACommerce.Authentication.TwoFactor.Email` | إرسال رموز OTP عبر البريد الإلكتروني |
| Nafath | `ACommerce.Authentication.TwoFactor.Nafath` | التحقق عبر تطبيق نفاذ السعودي |

---

## البنية المعمارية | Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Application Layer                             │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              ITwoFactorService (Abstractions)             │  │
│  └──────────────────────────────────────────────────────────┘  │
│                              ↑                                   │
│         ┌────────────────────┼────────────────────┐             │
│         │                    │                    │             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │  SMS Service │    │ Email Service│    │Nafath Service│      │
│  └──────────────┘    └──────────────┘    └──────────────┘      │
│         │                    │                    │             │
│         ↓                    ↓                    ↓             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │ SMS Provider │    │ Email Provider│   │ Nafath API   │      │
│  │ (Twilio, etc)│    │ (SMTP, etc)  │    │              │      │
│  └──────────────┘    └──────────────┘    └──────────────┘      │
└─────────────────────────────────────────────────────────────────┘
```

---

# ACommerce.Authentication.TwoFactor.SMS

## نظرة عامة | Overview

**المسار | Path:** `Authentication/ACommerce.Authentication.TwoFactor.SMS`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)
**الاعتماديات | Dependencies:**
- ACommerce.Authentication.Abstractions
- ACommerce.Messaging.SMS.Abstractions

---

## المكونات | Components

### SmsTwoFactorService

```csharp
public class SmsTwoFactorService : ITwoFactorService
{
    private readonly ISmsService _smsService;
    private readonly IOtpRepository _otpRepository;
    private readonly SmsTwoFactorOptions _options;
    private readonly ILogger<SmsTwoFactorService> _logger;

    public string ProviderName => "SMS";

    public SmsTwoFactorService(
        ISmsService smsService,
        IOtpRepository otpRepository,
        IOptions<SmsTwoFactorOptions> options,
        ILogger<SmsTwoFactorService> logger)
    {
        _smsService = smsService;
        _otpRepository = otpRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> SendCodeAsync(
        Guid userId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        // Check rate limiting
        var recentAttempts = await _otpRepository.GetRecentAttemptsAsync(
            userId, "SMS", TimeSpan.FromMinutes(5), cancellationToken);

        if (recentAttempts >= _options.MaxAttemptsPerWindow)
        {
            _logger.LogWarning(
                "Rate limit exceeded for SMS 2FA. UserId: {UserId}",
                userId);
            return Result.Failure("تم تجاوز الحد الأقصى للمحاولات. يرجى الانتظار قليلاً");
        }

        // Generate OTP
        var code = GenerateOtp();

        // Store OTP
        var otp = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = HashCode(code),
            Provider = "SMS",
            Destination = phoneNumber,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_options.CodeExpirationMinutes),
            CreatedAt = DateTime.UtcNow
        };

        await _otpRepository.AddAsync(otp, cancellationToken);

        // Send SMS
        var message = string.Format(_options.MessageTemplate, code);
        var result = await _smsService.SendAsync(phoneNumber, message, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to send SMS OTP to {Phone}", phoneNumber);
            return Result.Failure("فشل إرسال رمز التحقق. يرجى المحاولة لاحقاً");
        }

        _logger.LogInformation(
            "SMS OTP sent successfully to user {UserId}",
            userId);

        return Result.Success();
    }

    public async Task<Result<TwoFactorVerificationResult>> VerifyCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var otp = await _otpRepository.GetLatestValidAsync(
            userId, "SMS", cancellationToken);

        if (otp == null)
        {
            return Result<TwoFactorVerificationResult>.Failure("لا يوجد رمز تحقق صالح");
        }

        if (otp.ExpiresAt < DateTime.UtcNow)
        {
            return Result<TwoFactorVerificationResult>.Failure("انتهت صلاحية رمز التحقق");
        }

        if (otp.Attempts >= _options.MaxVerificationAttempts)
        {
            return Result<TwoFactorVerificationResult>.Failure(
                "تم تجاوز الحد الأقصى لمحاولات التحقق");
        }

        // Increment attempts
        otp.Attempts++;
        await _otpRepository.UpdateAsync(otp, cancellationToken);

        // Verify code
        if (!VerifyHash(code, otp.Code))
        {
            return Result<TwoFactorVerificationResult>.Failure("رمز التحقق غير صحيح");
        }

        // Mark as used
        otp.UsedAt = DateTime.UtcNow;
        await _otpRepository.UpdateAsync(otp, cancellationToken);

        _logger.LogInformation(
            "SMS OTP verified successfully for user {UserId}",
            userId);

        return Result<TwoFactorVerificationResult>.Success(
            new TwoFactorVerificationResult
            {
                UserId = userId,
                Provider = "SMS",
                VerifiedAt = DateTime.UtcNow
            });
    }

    public async Task<Result> EnableAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Implementation for enabling SMS 2FA
        return Result.Success();
    }

    public async Task<Result> DisableAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Implementation for disabling SMS 2FA
        return Result.Success();
    }

    private string GenerateOtp()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var number = BitConverter.ToUInt32(bytes, 0) % 1000000;
        return number.ToString("D6"); // 6-digit code
    }

    private static string HashCode(string code)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(code);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyHash(string code, string hash)
    {
        return HashCode(code) == hash;
    }
}
```

### SmsTwoFactorOptions

```csharp
public class SmsTwoFactorOptions
{
    public const string SectionName = "TwoFactor:SMS";

    /// <summary>
    /// مدة صلاحية الرمز بالدقائق
    /// </summary>
    public int CodeExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// أقصى عدد محاولات إرسال خلال فترة زمنية
    /// </summary>
    public int MaxAttemptsPerWindow { get; set; } = 3;

    /// <summary>
    /// أقصى عدد محاولات تحقق لكل رمز
    /// </summary>
    public int MaxVerificationAttempts { get; set; } = 3;

    /// <summary>
    /// قالب الرسالة
    /// {0} = الرمز
    /// </summary>
    public string MessageTemplate { get; set; } = "رمز التحقق الخاص بك هو: {0}";
}
```

---

# ACommerce.Authentication.TwoFactor.Email

## المكونات | Components

### EmailTwoFactorService

```csharp
public class EmailTwoFactorService : ITwoFactorService
{
    private readonly IEmailService _emailService;
    private readonly IOtpRepository _otpRepository;
    private readonly EmailTwoFactorOptions _options;
    private readonly ILogger<EmailTwoFactorService> _logger;

    public string ProviderName => "Email";

    public EmailTwoFactorService(
        IEmailService emailService,
        IOtpRepository otpRepository,
        IOptions<EmailTwoFactorOptions> options,
        ILogger<EmailTwoFactorService> logger)
    {
        _emailService = emailService;
        _otpRepository = otpRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> SendCodeAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken = default)
    {
        // Rate limiting check
        var recentAttempts = await _otpRepository.GetRecentAttemptsAsync(
            userId, "Email", TimeSpan.FromMinutes(5), cancellationToken);

        if (recentAttempts >= _options.MaxAttemptsPerWindow)
        {
            return Result.Failure("تم تجاوز الحد الأقصى للمحاولات");
        }

        // Generate OTP
        var code = GenerateOtp();

        // Store OTP
        var otp = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = HashCode(code),
            Provider = "Email",
            Destination = email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_options.CodeExpirationMinutes),
            CreatedAt = DateTime.UtcNow
        };

        await _otpRepository.AddAsync(otp, cancellationToken);

        // Send email
        var emailMessage = new EmailMessage
        {
            To = email,
            Subject = _options.EmailSubject,
            Body = string.Format(_options.EmailBodyTemplate, code),
            IsHtml = true
        };

        var result = await _emailService.SendAsync(emailMessage, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to send Email OTP to {Email}", email);
            return Result.Failure("فشل إرسال رمز التحقق");
        }

        return Result.Success();
    }

    public async Task<Result<TwoFactorVerificationResult>> VerifyCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        // Similar to SMS implementation
        var otp = await _otpRepository.GetLatestValidAsync(
            userId, "Email", cancellationToken);

        if (otp == null || otp.ExpiresAt < DateTime.UtcNow)
        {
            return Result<TwoFactorVerificationResult>.Failure("رمز التحقق غير صالح أو منتهي");
        }

        if (!VerifyHash(code, otp.Code))
        {
            otp.Attempts++;
            await _otpRepository.UpdateAsync(otp, cancellationToken);
            return Result<TwoFactorVerificationResult>.Failure("رمز التحقق غير صحيح");
        }

        otp.UsedAt = DateTime.UtcNow;
        await _otpRepository.UpdateAsync(otp, cancellationToken);

        return Result<TwoFactorVerificationResult>.Success(
            new TwoFactorVerificationResult
            {
                UserId = userId,
                Provider = "Email",
                VerifiedAt = DateTime.UtcNow
            });
    }

    // ... Enable/Disable methods similar to SMS
}
```

### EmailTwoFactorOptions

```csharp
public class EmailTwoFactorOptions
{
    public const string SectionName = "TwoFactor:Email";

    public int CodeExpirationMinutes { get; set; } = 10;
    public int MaxAttemptsPerWindow { get; set; } = 5;
    public int MaxVerificationAttempts { get; set; } = 3;
    public string EmailSubject { get; set; } = "رمز التحقق الخاص بك";
    public string EmailBodyTemplate { get; set; } = @"
        <div style='direction: rtl; font-family: Arial;'>
            <h2>رمز التحقق</h2>
            <p>رمز التحقق الخاص بك هو:</p>
            <h1 style='color: #2563eb; font-size: 32px;'>{0}</h1>
            <p>صالح لمدة 10 دقائق.</p>
        </div>";
}
```

---

# ACommerce.Authentication.TwoFactor.Nafath

## نظرة عامة | Overview

نفاذ هو نظام الهوية الرقمية الوطني في المملكة العربية السعودية. يسمح بالتحقق من هوية المستخدم عبر تطبيق نفاذ.

Nafath is the national digital identity system in Saudi Arabia. It allows verifying user identity through the Nafath app.

**المسار | Path:** `Authentication/ACommerce.Authentication.TwoFactor.Nafath`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)

---

## المكونات | Components

### NafathTwoFactorService

```csharp
public class NafathTwoFactorService : ITwoFactorService
{
    private readonly INafathApiClient _nafathClient;
    private readonly INafathSessionRepository _sessionRepository;
    private readonly NafathOptions _options;
    private readonly ILogger<NafathTwoFactorService> _logger;

    public string ProviderName => "Nafath";

    public NafathTwoFactorService(
        INafathApiClient nafathClient,
        INafathSessionRepository sessionRepository,
        IOptions<NafathOptions> options,
        ILogger<NafathTwoFactorService> logger)
    {
        _nafathClient = nafathClient;
        _sessionRepository = sessionRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// بدء جلسة نفاذ للتحقق
    /// </summary>
    public async Task<Result<NafathInitResponse>> InitiateAsync(
        string nationalId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Call Nafath API to initiate authentication
            var response = await _nafathClient.InitiateAuthAsync(
                nationalId,
                _options.ServiceId,
                cancellationToken);

            if (!response.IsSuccess)
            {
                return Result<NafathInitResponse>.Failure(response.ErrorMessage);
            }

            // Store session
            var session = new NafathSession
            {
                Id = Guid.NewGuid(),
                TransactionId = response.TransactionId,
                NationalId = nationalId,
                RandomCode = response.Random,
                Status = NafathStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_options.SessionExpirationMinutes),
                CreatedAt = DateTime.UtcNow
            };

            await _sessionRepository.AddAsync(session, cancellationToken);

            _logger.LogInformation(
                "Nafath session initiated. TransactionId: {TransactionId}",
                response.TransactionId);

            return Result<NafathInitResponse>.Success(new NafathInitResponse
            {
                TransactionId = response.TransactionId,
                RandomCode = response.Random,
                ExpiresAt = session.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate Nafath session");
            return Result<NafathInitResponse>.Failure("فشل بدء جلسة نفاذ");
        }
    }

    /// <summary>
    /// التحقق من حالة جلسة نفاذ
    /// </summary>
    public async Task<Result<NafathVerificationResult>> CheckStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByTransactionIdAsync(
            transactionId, cancellationToken);

        if (session == null)
        {
            return Result<NafathVerificationResult>.Failure("جلسة نفاذ غير موجودة");
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            return Result<NafathVerificationResult>.Failure("انتهت صلاحية جلسة نفاذ");
        }

        if (session.Status == NafathStatus.Completed)
        {
            return Result<NafathVerificationResult>.Success(
                new NafathVerificationResult
                {
                    TransactionId = transactionId,
                    NationalId = session.NationalId,
                    Status = NafathStatus.Completed,
                    VerifiedAt = session.VerifiedAt
                });
        }

        // Check status from Nafath API
        var statusResponse = await _nafathClient.CheckStatusAsync(
            transactionId, cancellationToken);

        // Update session status
        session.Status = statusResponse.Status;

        if (statusResponse.Status == NafathStatus.Completed)
        {
            session.VerifiedAt = DateTime.UtcNow;
        }

        await _sessionRepository.UpdateAsync(session, cancellationToken);

        return Result<NafathVerificationResult>.Success(
            new NafathVerificationResult
            {
                TransactionId = transactionId,
                NationalId = session.NationalId,
                Status = session.Status,
                VerifiedAt = session.VerifiedAt
            });
    }

    // ITwoFactorService implementation
    public async Task<Result> SendCodeAsync(
        Guid userId,
        string nationalId,
        CancellationToken cancellationToken = default)
    {
        var result = await InitiateAsync(nationalId, cancellationToken);

        if (result.IsFailure)
            return Result.Failure(result.Error!);

        // Store user association
        await _sessionRepository.AssociateUserAsync(
            result.Value!.TransactionId, userId, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<TwoFactorVerificationResult>> VerifyCodeAsync(
        Guid userId,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var result = await CheckStatusAsync(transactionId, cancellationToken);

        if (result.IsFailure)
            return Result<TwoFactorVerificationResult>.Failure(result.Error!);

        if (result.Value!.Status != NafathStatus.Completed)
        {
            return Result<TwoFactorVerificationResult>.Failure(
                $"حالة التحقق: {result.Value.Status}");
        }

        return Result<TwoFactorVerificationResult>.Success(
            new TwoFactorVerificationResult
            {
                UserId = userId,
                Provider = "Nafath",
                VerifiedAt = result.Value.VerifiedAt ?? DateTime.UtcNow
            });
    }
}
```

### NafathOptions

```csharp
public class NafathOptions
{
    public const string SectionName = "TwoFactor:Nafath";

    /// <summary>
    /// معرف الخدمة في نفاذ
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>
    /// رابط API نفاذ
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://nafath.api.elm.sa";

    /// <summary>
    /// مفتاح API
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// مدة صلاحية الجلسة بالدقائق
    /// </summary>
    public int SessionExpirationMinutes { get; set; } = 3;

    /// <summary>
    /// فترة الاستعلام عن الحالة (بالثواني)
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 3;
}
```

### NafathStatus

```csharp
public enum NafathStatus
{
    /// <summary>
    /// في انتظار تأكيد المستخدم
    /// </summary>
    Pending = 0,

    /// <summary>
    /// تم التحقق بنجاح
    /// </summary>
    Completed = 1,

    /// <summary>
    /// انتهت الصلاحية
    /// </summary>
    Expired = 2,

    /// <summary>
    /// تم الرفض من المستخدم
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// فشل التحقق
    /// </summary>
    Failed = 4
}
```

---

## تسجيل الخدمات | Service Registration

### SMS Two-Factor

```csharp
public static class SmsTwoFactorExtensions
{
    public static IServiceCollection AddSmsTwoFactor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmsTwoFactorOptions>(
            configuration.GetSection(SmsTwoFactorOptions.SectionName));

        services.AddScoped<ITwoFactorService, SmsTwoFactorService>();

        return services;
    }
}
```

### Email Two-Factor

```csharp
public static class EmailTwoFactorExtensions
{
    public static IServiceCollection AddEmailTwoFactor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailTwoFactorOptions>(
            configuration.GetSection(EmailTwoFactorOptions.SectionName));

        services.AddScoped<ITwoFactorService, EmailTwoFactorService>();

        return services;
    }
}
```

### Nafath Two-Factor

```csharp
public static class NafathTwoFactorExtensions
{
    public static IServiceCollection AddNafathTwoFactor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<NafathOptions>(
            configuration.GetSection(NafathOptions.SectionName));

        services.AddHttpClient<INafathApiClient, NafathApiClient>(client =>
        {
            var options = configuration
                .GetSection(NafathOptions.SectionName)
                .Get<NafathOptions>();

            client.BaseAddress = new Uri(options!.ApiBaseUrl);
            client.DefaultRequestHeaders.Add("X-API-Key", options.ApiKey);
        });

        services.AddScoped<ITwoFactorService, NafathTwoFactorService>();

        return services;
    }
}
```

### تفعيل جميع المزودين | Enable All Providers

```csharp
public static class TwoFactorExtensions
{
    public static IServiceCollection AddAllTwoFactorProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSmsTwoFactor(configuration);
        services.AddEmailTwoFactor(configuration);
        services.AddNafathTwoFactor(configuration);

        // Register provider factory
        services.AddScoped<ITwoFactorProviderFactory, TwoFactorProviderFactory>();

        return services;
    }
}

public interface ITwoFactorProviderFactory
{
    ITwoFactorService GetProvider(string providerName);
    IEnumerable<string> GetAvailableProviders();
}

public class TwoFactorProviderFactory : ITwoFactorProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _providers;

    public TwoFactorProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _providers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["SMS"] = typeof(SmsTwoFactorService),
            ["Email"] = typeof(EmailTwoFactorService),
            ["Nafath"] = typeof(NafathTwoFactorService)
        };
    }

    public ITwoFactorService GetProvider(string providerName)
    {
        if (!_providers.TryGetValue(providerName, out var providerType))
        {
            throw new ArgumentException($"Unknown 2FA provider: {providerName}");
        }

        return (ITwoFactorService)_serviceProvider.GetRequiredService(providerType);
    }

    public IEnumerable<string> GetAvailableProviders() => _providers.Keys;
}
```

---

## تكوين appsettings.json | Configuration

```json
{
  "TwoFactor": {
    "SMS": {
      "CodeExpirationMinutes": 5,
      "MaxAttemptsPerWindow": 3,
      "MaxVerificationAttempts": 3,
      "MessageTemplate": "رمز التحقق الخاص بك هو: {0}"
    },
    "Email": {
      "CodeExpirationMinutes": 10,
      "MaxAttemptsPerWindow": 5,
      "MaxVerificationAttempts": 3,
      "EmailSubject": "رمز التحقق الخاص بك",
      "EmailBodyTemplate": "<div>رمز التحقق: {0}</div>"
    },
    "Nafath": {
      "ServiceId": "your-service-id",
      "ApiBaseUrl": "https://nafath.api.elm.sa",
      "ApiKey": "your-api-key",
      "SessionExpirationMinutes": 3,
      "PollingIntervalSeconds": 3
    }
  }
}
```

---

## المراجع | References

- [NIST SP 800-63B - Digital Identity Guidelines](https://pages.nist.gov/800-63-3/sp800-63b.html)
- [Nafath Developer Portal](https://nafath.sa/)
- [OWASP Multi-Factor Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Multifactor_Authentication_Cheat_Sheet.html)
