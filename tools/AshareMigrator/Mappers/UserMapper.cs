using AshareMigrator.Legacy;
using AshareMigrator.Target;

namespace AshareMigrator.Mappers;

public static class UserMapper
{
    /// <summary>
    /// تحوّل LegacyUser + LegacyProfile (اختياري) + LegacyVendor (اختياري) إلى NewUser.
    /// الدور (role) يُحدَّد: vendor موجود → "owner"، غير ذلك → "customer".
    /// </summary>
    public static NewUser Map(LegacyUser src, LegacyProfile? profile, bool hasVendor)
    {
        var fullName = BuildFullName(profile, src.Username);
        return new NewUser
        {
            Id = src.Id,
            CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = src.UpdatedAt,
            IsDeleted = false,
            PhoneNumber = src.PhoneNumber ?? src.UserId,
            Email = string.IsNullOrWhiteSpace(src.Email) ? null : src.Email,
            FullName = fullName,
            NationalId = null,
            NafathVerified = false,
            IsActive = src.IsActive && !src.IsLocked,
            Role = hasVendor ? "owner" : "customer",
        };
    }

    public static NewProfile? MapProfile(LegacyProfile? src)
    {
        if (src == null) return null;
        return new NewProfile
        {
            Id = src.Id,
            CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = src.UpdatedAt,
            IsDeleted = false,
            UserId = src.UserId,
            FirstName = src.FirstName,
            LastName = src.LastName,
            AvatarUrl = src.AvatarUrl,
            Bio = src.Bio,
            City = src.City,
            Country = src.Country,
            PreferredLanguage = src.PreferredLanguage ?? "ar",
        };
    }

    private static string BuildFullName(LegacyProfile? profile, string fallback)
    {
        if (profile == null) return fallback;
        var first = profile.FirstName?.Trim() ?? "";
        var last = profile.LastName?.Trim() ?? "";
        var full = $"{first} {last}".Trim();
        return string.IsNullOrWhiteSpace(full) ? fallback : full;
    }
}
