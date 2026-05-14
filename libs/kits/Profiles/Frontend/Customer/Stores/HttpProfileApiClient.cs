using ACommerce.ClientHost.KitApi;
using ACommerce.Kits.Profiles.Operations;

namespace ACommerce.Kits.Profiles.Frontend.Customer.Stores;

/// <summary>
/// تنفيذ افتراضيّ يَستهلك <see cref="KitHttpClient"/>. DTO يُحقّق
/// <see cref="IUserProfile"/> مباشرة (Law 6) — pages/store تَرى interface فقط.
/// </summary>
public sealed class HttpProfileApiClient : IProfileApiClient
{
    private const string Kit = "profiles";
    private readonly KitHttpClient _http;

    public HttpProfileApiClient(KitHttpClient http) => _http = http;

    public async Task<IUserProfile?> GetMineAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync<ProfileDto>(Kit, "/me/profile", ct);
        return res.Success ? res.Data : null;
    }

    public async Task<IUserProfile?> UpdateAsync(IUserProfile next,
                                                 IReadOnlyDictionary<string, object?>? attributes = null,
                                                 CancellationToken ct = default)
    {
        // الحُمولَة المُسَطَّحَة الَّتي يَتَوَقَّعها <c>ProfilesController.UpdateBody</c>:
        // FullName/Phone/Email/City/AvatarUrl + Attributes اختياري.
        var body = new
        {
            fullName  = next.FullName,
            phone     = next.Phone,
            email     = next.Email,
            city      = next.City,
            avatarUrl = next.AvatarUrl,
            attributes = attributes,
        };
        var res = await _http.PutAsync<ProfileDto>(Kit, "/me/profile", body, ct);
        return res.Success ? res.Data : null;
    }

    private sealed record ProfileDto(
        string Id,
        string FullName,
        string Phone,
        bool   PhoneVerified,
        string? Email,
        bool   EmailVerified,
        string  City,
        string? AvatarUrl,
        DateTime MemberSince) : IUserProfile;
}
