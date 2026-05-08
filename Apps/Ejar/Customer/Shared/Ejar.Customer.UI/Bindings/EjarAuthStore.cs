using ACommerce.Kits.Auth.Frontend.Customer.Stores;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IAuthStore"/> لإيجار. يَدلّع للـ <see cref="IAuthApiClient"/>
/// لشكل الـ HTTP، ويُحَدِّث <see cref="AppStore"/> بالـ JWT + الهوية. لا
/// shape knowledge هنا — Kit api client يَملك ذلك مَركزياً.
/// </summary>
public sealed class EjarAuthStore : IAuthStore, IDisposable
{
    private readonly AppStore _app;
    private readonly IAuthApiClient _api;

    public EjarAuthStore(AppStore app, IAuthApiClient api)
    {
        _app = app;
        _api = api;
        _app.OnChanged += FireChanged;
    }

    public bool IsAuthenticated => _app.Auth.IsAuthenticated;
    public string? UserId      => _app.Auth.UserId?.ToString();
    public string? FullName    => _app.Auth.FullName;
    public bool IsBusy { get; private set; }
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
            _app.Auth.UserId      = ParseUserGuid(r.UserId!);
            _app.Auth.FullName    = r.FullName ?? "—";
            _app.Auth.Phone       = phone;
            _app.Auth.AccessToken = r.Token!;
            _app.NotifyChanged();
        }
        finally { IsBusy = false; FireChanged(); }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        IsBusy = true; FireChanged();
        try
        {
            try { await _api.LogoutAsync(ct); } catch { }
            _app.Auth.UserId      = null;
            _app.Auth.FullName    = null;
            _app.Auth.Phone       = null;
            _app.Auth.AccessToken = null;
            _app.NotifyChanged();
        }
        finally { IsBusy = false; FireChanged(); }
    }

    /// <summary>
    /// يُحَوِّل userId خادميّ إلى Guid. لو كان Guid فعلاً نُرجِعه. غير ذلك
    /// نُولِّد Guid حتميّاً من SHA-1 للنصّ — نَفس الـ id ⇒ نَفس Guid دائماً،
    /// فلا تَتَكاثر السجلّات المحلّيّة. هذا يُمَكِّن APIs لا تَستخدم Guid.
    /// </summary>
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
    public void Dispose() => _app.OnChanged -= FireChanged;
}
