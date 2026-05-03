using ACommerce.Kits.Profiles.Frontend.Customer.Stores;
using ACommerce.Kits.Profiles.Operations;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IProfileStore"/> لإيجار. يَجلب من <c>GET /me/profile</c>
/// ويَدفع تعديلات بـ <c>PUT /me/profile</c>. يَكشف <see cref="IUserProfile"/>
/// (interface من Profiles.Operations) — الصفحات لا تَرى أيّ DTO خادميّ.
/// </summary>
public sealed class EjarProfileStore : IProfileStore
{
    private readonly ApiReader _api;

    public EjarProfileStore(ApiReader api) => _api = api;

    public IUserProfile? Current { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _api.GetAsync<ProfileDto>("/me/profile", ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                Current = env.Data;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task UpdateAsync(IUserProfile next, CancellationToken ct = default)
    {
        var env = await _api.PostAsync<ProfileDto>("/me/profile", next, ct);
        // PUT لا يَرجع envelope مختلف — نَستهلك Success فقط
        if (env.Operation.Status == "Success" && env.Data is not null)
            Current = env.Data;
        Changed?.Invoke();
    }

    /// <summary>DTO خادميّ يُحقّق <see cref="IUserProfile"/> مباشرةً (Law 6).</summary>
    private sealed record ProfileDto(
        string Id,
        string FullName,
        string Phone,
        bool   PhoneVerified,
        string? Email,
        bool   EmailVerified,
        string? City,
        string? AvatarUrl,
        DateTime MemberSince) : IUserProfile;
}
