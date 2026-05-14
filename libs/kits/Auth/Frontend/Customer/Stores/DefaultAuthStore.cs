using ACommerce.Client.Operations;
using ACommerce.ClientHost.Auth;

namespace ACommerce.Kits.Auth.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتراضيّ لـ <see cref="IAuthStore"/> — OAM-shaped (F61).
/// كلّ سُلوك يُمَثَّل بِقَيد محاسبيّ يُرسَل عَبر <see cref="ITemplateEngine"/>.
/// مَكاسِب OAM: compositions تَحقن مُعتَرضات (telemetry، captcha، rate-limit،
/// MFA prompt) عَلى op type بدون لَمس الـ store. الـ envelope يُحَدِّث
/// <see cref="IClientAuthState"/> داخِليّاً عِند نَجاح verify.
/// </summary>
public sealed class DefaultAuthStore : IAuthStore, IDisposable
{
    private readonly ITemplateEngine _engine;
    private readonly IClientAuthState _state;

    public DefaultAuthStore(ITemplateEngine engine, IClientAuthState state)
    {
        _engine = engine;
        _state = state;
        _state.OnChanged += FireChanged;
    }

    public bool    IsAuthenticated => _state.IsAuthenticated;
    public string? UserId          => _state.UserId?.ToString();
    public string? FullName        => _state.FullName;
    public bool    IsBusy   { get; private set; }
    public string? LastError { get; private set; }
    public string? LastChallengeId { get; private set; }
    public IReadOnlyDictionary<string, string>? LastProviderData { get; private set; }
    public event Action? Changed;

    public async Task RequestOtpAsync(string phone, CancellationToken ct = default)
    {
        IsBusy = true; LastError = null;
        LastChallengeId = null; LastProviderData = null;
        FireChanged();
        try
        {
            var env = await _engine.ExecuteAsync<OtpRequestDto>(
                AuthOps.RequestOtp(phone),
                payload: new { phone },
                ct: ct);
            if (env.Operation.Status != "Success")
            {
                LastError = env.Error?.Message ?? "otp_request_failed";
                return;
            }
            LastChallengeId  = env.Data?.ChallengeId;
            LastProviderData = env.Data?.ProviderData;
        }
        finally { IsBusy = false; FireChanged(); }
    }

    public async Task VerifyOtpAsync(string phone, string code, CancellationToken ct = default)
    {
        IsBusy = true; LastError = null; FireChanged();
        try
        {
            var env = await _engine.ExecuteAsync<AuthVerifyDto>(
                AuthOps.VerifyOtp(phone, code),
                payload: new { phone, code },
                ct: ct);
            if (env.Operation.Status != "Success" || env.Data is null
                || string.IsNullOrEmpty(env.Data.Token) || string.IsNullOrEmpty(env.Data.UserId))
            {
                LastError = env.Error?.Message ?? "otp_verify_failed";
                return;
            }
            _state.UserId      = ParseUserGuid(env.Data.UserId!);
            _state.FullName    = env.Data.Name ?? "—";
            _state.Phone       = phone;
            _state.AccessToken = env.Data.Token!;
            _state.Role        = env.Data.Role;
            _state.NotifyChanged();
        }
        finally { IsBusy = false; FireChanged(); }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        IsBusy = true; FireChanged();
        try
        {
            try { await _engine.ExecuteAsync<object>(AuthOps.SignOut(), ct: ct); } catch { }
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

    private sealed record OtpRequestDto(
        string? Masked,
        int? ExpiresInSeconds,
        string? ChallengeId,
        IReadOnlyDictionary<string, string>? ProviderData);
    private sealed record AuthVerifyDto(string? Token, string? UserId, string? Name, string? Phone, string? Role);
}
