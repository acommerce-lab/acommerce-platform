using ACommerce.Kits.Profiles.Operations;

namespace ACommerce.Kits.Profiles.Backend;

public interface IProfileStore
{
    Task<IUserProfile?> GetAsync(string userId, CancellationToken ct);

    /// <summary>تطبيق PATCH (null = "لا تغيِّر"). F6: لا save.</summary>
    Task<bool> UpdateNoSaveAsync(string userId, ProfileUpdate patch, CancellationToken ct);
}

public sealed record ProfileUpdate(
    string? FullName,
    string? Phone,
    string? Email,
    string? City,
    string? AvatarUrl,
    /// <summary>JSON خام لِسِمات ديناميكِيَّة لِلبروفايل (مَفاتيح القالَب
    /// → قِيَم). <c>null</c> = "لا تَلمَسها". المَتَحَكِّم يَفُكّ Dictionary
    /// وَ يُسَلسِله قَبل بِناء PATCH (نَفس صياغَة Listings).</summary>
    string? AttributesJson = null);
