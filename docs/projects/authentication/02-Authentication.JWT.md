# ACommerce.Authentication.JWT

## نظرة عامة
تنفيذ مزود مصادقة JWT البسيط. يوفر إنشاء وتحقق من tokens مع تكامل كامل مع ASP.NET Core Authentication.

## الموقع
`/Authentication/ACommerce.Authentication.JWT`

## التبعيات
- `ACommerce.Authentication.Abstractions`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.IdentityModel.Tokens`

---

## JwtAuthenticationProvider

### الوصف
تنفيذ `IAuthenticationProvider` باستخدام JWT (JSON Web Tokens):

```csharp
public class JwtAuthenticationProvider : IAuthenticationProvider
{
    public string ProviderName => "JWT";
}
```

### الميزات المدعومة

| الطريقة | مدعومة | ملاحظة |
|---------|--------|--------|
| `AuthenticateAsync` | نعم | إنشاء Access Token |
| `ValidateTokenAsync` | نعم | التحقق من صحة Token |
| `RefreshAsync` | لا | JWT بسيط لا يدعم Refresh Tokens |
| `RevokeTokenAsync` | لا | JWT stateless - استخدم blacklist أو OpenIddict |

### كيفية إنشاء Token
```csharp
var result = await jwtProvider.AuthenticateAsync(new AuthenticationRequest
{
    Identifier = userId,
    Claims = new Dictionary<string, string>
    {
        ["role"] = "Admin",
        ["email"] = "user@example.com"
    }
});

// result.AccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

### Claims المُضافة تلقائياً
- `sub` (Subject): معرف المستخدم
- `jti` (JWT ID): معرف فريد للـ Token
- `iat` (Issued At): وقت الإنشاء

---

## JwtOptions

### التكوين
```csharp
public class JwtOptions
{
    public const string SectionName = "Authentication:JWT";

    // مفتاح التوقيع (32 حرف على الأقل)
    public string SecretKey { get; set; }

    // مُصدر الـ Token
    public string Issuer { get; set; }

    // الجمهور المستهدف
    public string Audience { get; set; }

    // مدة صلاحية Access Token (افتراضي: ساعة)
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);
}
```

### مثال appsettings.json
```json
{
  "Authentication": {
    "JWT": {
      "SecretKey": "your-secret-key-minimum-32-characters-long",
      "Issuer": "https://api.yourapp.com",
      "Audience": "https://yourapp.com",
      "AccessTokenLifetime": "01:00:00"
    }
  }
}
```

---

## تسجيل الخدمات

### AddJwtAuthentication (كامل)
تسجيل المزود مع تكوين ASP.NET Core Authentication:

```csharp
// من appsettings.json
builder.Services.AddJwtAuthentication(builder.Configuration);

// أو يدوياً
builder.Services.AddJwtAuthentication(options =>
{
    options.SecretKey = "your-secret-key-minimum-32-characters-long";
    options.Issuer = "https://api.yourapp.com";
    options.Audience = "https://yourapp.com";
    options.AccessTokenLifetime = TimeSpan.FromHours(2);
});
```

### AddJwtAuthenticationProvider (المزود فقط)
تسجيل المزود بدون middleware (للعملاء):

```csharp
// للتطبيقات التي تحتاج فقط إنشاء/التحقق من tokens
builder.Services.AddJwtAuthenticationProvider(builder.Configuration);
```

---

## تكوين ASP.NET Core

### ما يتم تسجيله تلقائياً
1. **JwtBearerAuthentication**: كـ Default Scheme
2. **TokenValidationParameters**:
   - التحقق من المُصدر (Issuer)
   - التحقق من الجمهور (Audience)
   - التحقق من التوقيع
   - التحقق من الصلاحية (لا ClockSkew)
3. **Authorization Services**
4. **Custom Events**:
   - إضافة `Token-Expired` header عند انتهاء الصلاحية
   - JSON response للـ 401/403

### استخدام في Controller
```csharp
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SecureController : ControllerBase
{
    [HttpGet]
    public IActionResult GetSecureData()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Ok(new { userId });
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetAdminData()
    {
        return Ok("Admin only data");
    }
}
```

### Middleware في Program.cs
```csharp
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
```

---

## أمثلة الاستخدام

### إنشاء Token
```csharp
public class AuthController : ControllerBase
{
    private readonly IAuthenticationProvider _authProvider;

    public AuthController(IAuthenticationProvider authProvider)
    {
        _authProvider = authProvider;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // التحقق من البيانات (من قاعدة البيانات)
        var user = await _userService.ValidateCredentialsAsync(
            request.Email, request.Password);

        if (user == null)
            return Unauthorized();

        // إنشاء Token
        var result = await _authProvider.AuthenticateAsync(new AuthenticationRequest
        {
            Identifier = user.Id.ToString(),
            Claims = new Dictionary<string, string>
            {
                ["email"] = user.Email,
                ["role"] = user.Role
            }
        });

        if (!result.Success)
            return BadRequest(result.Error);

        return Ok(new
        {
            token = result.AccessToken,
            expiresAt = result.ExpiresAt
        });
    }
}
```

### التحقق من Token
```csharp
var validationResult = await _authProvider.ValidateTokenAsync(token);

if (validationResult.IsValid)
{
    var userId = validationResult.UserId;
    var claims = validationResult.Claims;
}
else
{
    Console.WriteLine($"Invalid token: {validationResult.Error}");
}
```

---

## بنية الملفات
```
ACommerce.Authentication.JWT/
├── JwtAuthenticationProvider.cs    # المزود + JwtOptions
├── ServiceCollectionExtensions.cs  # تسجيل الخدمات
├── InMemoryUserProvider.cs         # للتجربة
├── InMemoryRoleProvider.cs         # للتجربة
└── InMemoryClaimProvider.cs        # للتجربة
```

---

## ملاحظات تقنية

1. **Stateless**: JWT tokens لا يمكن إلغاؤها (استخدم blacklist)
2. **No Refresh Token**: للحصول على Refresh Token استخدم OpenIddict
3. **SecretKey**: يجب أن يكون 32 حرف على الأقل
4. **HTTPS**: مُفعَّل افتراضياً (`RequireHttpsMetadata = true`)
5. **ClockSkew = Zero**: لا تسامح مع انتهاء الصلاحية
6. **Validation on Start**: التحقق من الإعدادات عند بدء التطبيق

---

## القيود

- لا يدعم Refresh Tokens
- لا يمكن إلغاء Tokens (stateless)
- للسيناريوهات المتقدمة استخدم `ACommerce.Authentication.OpenIddict`
