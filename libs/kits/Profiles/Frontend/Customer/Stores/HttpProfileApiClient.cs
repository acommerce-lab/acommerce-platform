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

    public async Task<IUserProfile?> UpdateAsync(IUserProfile next, CancellationToken ct = default)
    {
        var res = await _http.PutAsync<ProfileDto>(Kit, "/me/profile", next, ct);
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
