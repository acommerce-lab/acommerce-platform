using AshareMigrator.Legacy;
using AshareMigrator.Target;

namespace AshareMigrator.Mappers;

public static class UserMapper
{
    /// <summary>
    /// يبني NewUser من LegacyProfile (لا يوجد جدول Users في المصدر).
    /// Profile.UserId نص — يُحوَّل إلى Guid؛ إذا فشل يُستخدم Profile.Id.
    /// </summary>
    public static NewUser MapFromProfile(LegacyProfile src, bool isOwner)
    {
        var userId = ParseUserId(src);
        return new NewUser
        {
            Id = userId,
            CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = src.UpdatedAt,
            IsDeleted = false,
            PhoneNumber = src.PhoneNumber ?? userId.ToString(),
            Email = string.IsNullOrWhiteSpace(src.Email) ? null : src.Email,
            FullName = src.FullName,
            NationalId = null,
            NafathVerified = false,
            IsActive = src.IsActive,
            Role = isOwner ? "owner" : "customer",
        };
    }

    public static NewProfile? MapProfile(LegacyProfile? src)
    {
        if (src == null) return null;
        var userId = ParseUserId(src);
        var (first, last) = SplitName(src.FullName);
        return new NewProfile
        {
            Id = src.Id,
            CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = src.UpdatedAt,
            IsDeleted = false,
            UserId = userId,
            FirstName = first,
            LastName = last,
            AvatarUrl = src.Avatar,
            City = src.City,
            Country = src.Country,
            PreferredLanguage = "ar",
        };
    }

    public static Guid ParseUserId(LegacyProfile p)
        => Guid.TryParse(p.UserId, out var uid) ? uid : p.Id;

    private static (string?, string?) SplitName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return (null, null);
        var parts = fullName.Trim().Split(' ', 2);
        return parts.Length == 1 ? (parts[0], null) : (parts[0], parts[1]);
    }
}
