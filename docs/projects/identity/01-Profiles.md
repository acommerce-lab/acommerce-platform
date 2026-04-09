# ACommerce.Profiles

## نظرة عامة
مكتبة الملفات الشخصية (Profiles). تدعم أنواع متعددة: عملاء، بائعين، مدراء، موظفين، دعم فني.

## الموقع
`/Identity/ACommerce.Profiles`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## الكيانات (Entities)

### Profile
الملف الشخصي:

```csharp
public class Profile : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // الربط بالمستخدم
    public required string UserId { get; set; }
    public ProfileType Type { get; set; }

    // البيانات الأساسية
    public string? FullName { get; set; }
    public string? BusinessName { get; set; }  // للبائعين
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }

    // العنوان
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Coordinates { get; set; }

    // الحالة
    public bool IsActive { get; set; } = true;
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }

    // بيانات إضافية
    [NotMapped]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
```

---

## التعدادات (Enums)

### ProfileType

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Customer` | 1 | عميل |
| `Vendor` | 2 | بائع / تاجر |
| `Admin` | 3 | مدير النظام |
| `Employee` | 4 | موظف |
| `Support` | 5 | دعم فني |

---

## DTOs

### CreateProfileDto
```csharp
public class CreateProfileDto
{
    public required string UserId { get; set; }
    public ProfileType Type { get; set; }
    public string? FullName { get; set; }
    public string? BusinessName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}
```

### UpdateProfileDto
```csharp
public class UpdateProfileDto
{
    public string? FullName { get; set; }
    public string? BusinessName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
}
```

### ProfileResponseDto
```csharp
public class ProfileResponseDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public ProfileType Type { get; set; }
    public string? FullName { get; set; }
    public string? BusinessName { get; set; }
    public string? Avatar { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## الأحداث (Events)

```csharp
public record ProfileCreatedEvent(Guid ProfileId, string UserId, ProfileType Type);
public record ProfileUpdatedEvent(Guid ProfileId, string UserId);
public record ProfileVerifiedEvent(Guid ProfileId, string UserId);
```

---

## بنية الملفات
```
Identity/
├── ACommerce.Profiles/
│   ├── Entities/
│   │   └── Profile.cs
│   ├── DTOs/
│   │   ├── CreateProfileDto.cs
│   │   ├── UpdateProfileDto.cs
│   │   └── ProfileResponseDto.cs
│   ├── Enums/
│   │   └── ProfileType.cs
│   └── Events/
│       └── ProfileEvents.cs
└── ACommerce.Profiles.Api/
    └── Controllers/
        └── ProfilesController.cs
```

---

## مثال استخدام

### إنشاء بروفايل عميل
```csharp
var profile = new Profile
{
    UserId = userId,
    Type = ProfileType.Customer,
    FullName = "أحمد محمد",
    PhoneNumber = "+966501234567",
    Email = "ahmed@example.com",
    City = "الرياض",
    Country = "SA"
};

await profileRepository.AddAsync(profile);
```

### إنشاء بروفايل بائع
```csharp
var vendorProfile = new Profile
{
    UserId = userId,
    Type = ProfileType.Vendor,
    FullName = "محمد أحمد",
    BusinessName = "متجر التقنية",
    PhoneNumber = "+966501234568",
    Email = "store@example.com",
    City = "جدة",
    Country = "SA"
};

await profileRepository.AddAsync(vendorProfile);
```

### توثيق بائع
```csharp
profile.IsVerified = true;
profile.VerifiedAt = DateTime.UtcNow;

await profileRepository.UpdateAsync(profile);
```

---

## ملاحظات تقنية

1. **Multi-Type**: دعم أنواع متعددة من البروفايلات
2. **Vendor Support**: حقول إضافية للبائعين (BusinessName)
3. **Verification**: نظام توثيق للبائعين
4. **Geolocation**: دعم الإحداثيات الجغرافية
5. **Metadata**: بيانات إضافية مرنة
6. **Events**: أحداث للتكامل مع النظام
