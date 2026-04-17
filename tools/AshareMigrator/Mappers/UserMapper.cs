using AshareMigrator.Legacy;
using AshareMigrator.Target;

namespace AshareMigrator.Mappers;

public static class UserMapper
{
    /// <summary>
    /// يبني NewUser من LegacyProfile (لا يوجد جدول Users في المصدر).
    /// PhoneNumber يُعبَّأ بـ UserId.ToString() كقيمة placeholder.
    /// </summary>
    public static NewUser MapFromProfile(LegacyProfile src, bool isOwner) => new()
    {
        Id = src.UserId,
        CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
        UpdatedAt = src.UpdatedAt,
        IsDeleted = false,
        PhoneNumber = src.UserId.ToString(),
        Email = null,
        FullName = BuildFullName(src),
        NationalId = null,
        NafathVerified = false,
        IsActive = true,
        Role = isOwner ? "owner" : "customer",
    };

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

    private static string? BuildFullName(LegacyProfile profile)
    {
        var first = profile.FirstName?.Trim() ?? "";
        var last = profile.LastName?.Trim() ?? "";
        var full = $"{first} {last}".Trim();
        return string.IsNullOrWhiteSpace(full) ? null : full;
    }
}
