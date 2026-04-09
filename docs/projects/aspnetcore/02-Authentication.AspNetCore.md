# ACommerce.Authentication.AspNetCore

## نظرة عامة
تكامل نظام المصادقة مع ASP.NET Core. يوفر Controllers وValidators وDTOs جاهزة للاستخدام.

## الموقع
`/AspNetCore/ACommerce.Authentication.AspNetCore`

## التبعيات
- `ACommerce.Authentication.Abstractions`
- `ACommerce.Authentication.Users.Abstractions`
- `ACommerce.Messaging.Abstractions`
- `FluentValidation.AspNetCore`

---

## Controllers

### AuthenticationController
عمليات تسجيل الدخول والخروج:

```csharp
[Route("api/auth")]
public class AuthenticationController
{
    [HttpPost("login")]      // تسجيل الدخول
    [HttpPost("logout")]     // تسجيل الخروج
    [HttpPost("refresh")]    // تجديد Token
}
```

### UsersController
إدارة المستخدمين:

```csharp
[Route("api/users")]
public class UsersController
{
    [HttpGet]           // قائمة المستخدمين
    [HttpGet("{id}")]   // مستخدم محدد
    [HttpPost]          // إنشاء مستخدم
    [HttpPut("{id}")]   // تحديث مستخدم
    [HttpDelete("{id}")] // حذف مستخدم

    // عمليات كلمة المرور
    [HttpPost("forgot-password")]
    [HttpPost("reset-password")]
    [HttpPost("change-password")]
    [HttpPost("confirm-email")]
}
```

### RolesController
إدارة الأدوار والصلاحيات:

```csharp
[Route("api/roles")]
public class RolesController
{
    [HttpGet]               // قائمة الأدوار
    [HttpGet("{id}")]       // دور محدد
    [HttpPost]              // إنشاء دور
    [HttpPut("{id}")]       // تحديث دور
    [HttpDelete("{id}")]    // حذف دور

    // إدارة المستخدمين في الأدوار
    [HttpPost("{roleId}/users")]
    [HttpDelete("{roleId}/users/{userId}")]

    // إدارة الصلاحيات (Claims)
    [HttpGet("{roleId}/claims")]
    [HttpPost("{roleId}/claims")]
    [HttpDelete("{roleId}/claims")]
}
```

---

## DTOs

### Users DTOs
```csharp
public class CreateUserDto
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
}

public class UpdateUserDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
}

public class ChangePasswordDto
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
}

public class ForgotPasswordDto
{
    public required string Email { get; set; }
}

public class ResetPasswordDto
{
    public required string Email { get; set; }
    public required string Token { get; set; }
    public required string NewPassword { get; set; }
}

public class ConfirmEmailDto
{
    public required string Email { get; set; }
    public required string Token { get; set; }
}

public class UserResponseDto
{
    public string Id { get; set; }
    public string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public List<string> Roles { get; set; }
}
```

### Roles DTOs
```csharp
public class CreateRoleDto
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateRoleDto
{
    public string? Description { get; set; }
}

public class AddUserToRoleDto
{
    public required string UserId { get; set; }
}

public class RoleResponseDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public List<ClaimDto> Claims { get; set; }
}
```

### Claims DTOs
```csharp
public class AddClaimDto
{
    public required string Type { get; set; }
    public required string Value { get; set; }
}

public class RemoveClaimDto
{
    public required string Type { get; set; }
    public required string Value { get; set; }
}

public class ClaimDto
{
    public string Type { get; set; }
    public string Value { get; set; }
}
```

---

## Validators

جميع DTOs لديها Validators باستخدام FluentValidation:

```csharp
public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
            .EmailAddress().WithMessage("صيغة البريد غير صحيحة");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة")
            .MinimumLength(8).WithMessage("يجب أن تكون 8 أحرف على الأقل");
    }
}
```

---

## بنية الملفات
```
ACommerce.Authentication.AspNetCore/
├── Controllers/
│   ├── AuthenticationController.cs
│   ├── UsersController.cs
│   └── RolesController.cs
├── DTOs/
│   ├── Users/
│   │   ├── CreateUserDto.cs
│   │   ├── UpdateUserDto.cs
│   │   └── ...
│   ├── Roles/
│   │   ├── CreateRoleDto.cs
│   │   └── ...
│   └── Claims/
│       ├── AddClaimDto.cs
│       └── ...
├── Validators/
│   ├── Users/
│   │   └── CreateUserDtoValidator.cs
│   └── Roles/
│       └── CreateRoleDtoValidator.cs
├── Services/
│   └── MessagingAuthenticationEventPublisher.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

---

## تسجيل الخدمات

```csharp
services.AddACommerceAuthentication(options =>
{
    options.EnableMessaging = true;  // نشر الأحداث عبر Message Bus
});
```

---

## مثال استخدام

### تسجيل مستخدم جديد
```http
POST /api/users
Content-Type: application/json

{
    "email": "user@example.com",
    "password": "SecurePass123!",
    "firstName": "أحمد",
    "lastName": "محمد"
}
```

### تسجيل الدخول
```http
POST /api/auth/login
Content-Type: application/json

{
    "email": "user@example.com",
    "password": "SecurePass123!"
}
```

### إنشاء دور وإضافة صلاحيات
```http
POST /api/roles
Content-Type: application/json

{
    "name": "Admin",
    "description": "مدير النظام"
}

POST /api/roles/{roleId}/claims
Content-Type: application/json

{
    "type": "permission",
    "value": "users.manage"
}
```

---

## ملاحظات تقنية

1. **FluentValidation**: استخدام FluentValidation لجميع DTOs
2. **Messaging Integration**: نشر أحداث المصادقة عبر Message Bus
3. **Role-Based Auth**: دعم RBAC كامل
4. **Claims-Based**: دعم Permissions عبر Claims
