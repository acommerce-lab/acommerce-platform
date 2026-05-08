using ACommerce.ClientHost.Auth;

namespace ACommerce.Kits.Auth.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتراضيّ لـ <see cref="IAuthStore"/> يُحَوِّل المَطلَب لـ
/// <see cref="IAuthApiClient"/> ويُحَدِّث <see cref="IClientAuthState"/>
/// المُسَجَّل في الـ ClientHost. التَطبيقات لا تَحتاج كتابة Binding خاصّ
/// إلا لو احتاجت سُلوكاً مُختلفاً (multi-tenant، MFA، …).
/// </summary>
public sealed class DefaultAuthStore : IAuthStore, IDisposable
{
    private readonly IClientAuthState _state;
    private readonly IAuthApiClient _api;

    public DefaultAuthStore(IClientAuthState state, IAuthApiClient api)
    {
        _state = state;
        _api = api;
        _state.OnChanged += FireChanged;
    }

    public bool    IsAuthenticated => _state.IsAuthenticated;
    public string? UserId          => _state.UserId?.ToString();
    public string? FullName        => _state.FullName;
    public bool    IsBusy   { get; private set; }
    public string? LastError { get; private set; }
    public event Action? Changed;

    public async Task RequestOtpAsync(string phone, CancellationToken ct = default)
    {
        IsBusy = true; LastError = null; FireChanged();
        try
        {
            var r = await _api.RequestOtpAsync(phone, ct);
            if (!r.Success) LastError = r.Error ?? "otp_request_failed";
        }
        finally { IsBusy = false; FireChanged(); }
    }

    public async Task VerifyOtpAsync(string phone, string code, CancellationToken ct = default)
    {
        IsBusy = true; LastError = null; FireChanged();
        try
        {
            var r = await _api.VerifyOtpAsync(phone, code, ct);
            if (!r.Success || string.IsNullOrEmpty(r.Token) || string.IsNullOrEmpty(r.UserId))
            {
                LastError = r.Error ?? "otp_verify_failed";
                return;
            }
            _state.UserId      = ParseUserGuid(r.UserId!);
            _state.FullName    = r.FullName ?? "—";
            _state.Phone       = phone;
            _state.AccessToken = r.Token!;
            _state.Role        = r.Role;
            _state.NotifyChanged();
        }
        finally { IsBusy = false; FireChanged(); }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        IsBusy = true; FireChanged();
        try
        {
            try { await _api.LogoutAsync(ct); } catch { }
            _state.UserId      = null;
            _state.FullName    = null;
            _state.Phone       = null;
            _state.AccessToken = null;
            _state.Role        = null;
            _state.NotifyChanged();
        }
        finally { IsBusy = false; FireChanged(); }
    }

    private static Guid ParseUserGuid(string raw)
    {
        if (Guid.TryParse(raw, out var g)) return g;
        var bytes = System.Security.Cryptography.SHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw));
        var guidBytes = new byte[16];
        Array.Copy(bytes, guidBytes, 16);
        return new Guid(guidBytes);
    }

    private void FireChanged() => Changed?.Invoke();
    public void Dispose() => _state.OnChanged -= FireChanged;
}
