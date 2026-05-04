using ACommerce.Kits.Auth.Frontend.Customer.Stores;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IAuthStore"/> لإيجار. يَلفّ <see cref="AppStore"/> +
/// <see cref="ApiReader"/>. لا يَستحدث state — الـ AppStore يبقى مصدر
/// الحقيقة الوحيد للـ JWT والـ user info (يَستهلكه
/// <c>EjarAuthenticationStateProvider</c> أيضاً).
/// </summary>
public sealed class EjarAuthStore : IAuthStore, IDisposable
{
    private readonly AppStore _app;
    private readonly ApiReader _api;

    public EjarAuthStore(AppStore app, ApiReader api)
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
            var env = await _api.PostAsync<object>("/auth/otp/request", new { phone }, ct);
            if (env.Operation.Status != "Success")
                LastError = env.Error?.Message ?? env.Operation?.ErrorMessage ?? "otp_request_failed";
        }
        finally { IsBusy = false; FireChanged(); }
    }

    public async Task VerifyOtpAsync(string phone, string code, CancellationToken ct = default)
    {
        IsBusy = true; LastError = null; FireChanged();
        try
        {
            var env = await _api.PostAsync<AuthResponse>("/auth/otp/verify", new { phone, code }, ct);
            if (env.Operation.Status != "Success" || env.Data is null)
            {
                LastError = env.Error?.Message ?? env.Operation?.ErrorMessage ?? "otp_verify_failed";
                return;
            }
            // AppStore يبقى المصدر الوحيد — نُحدّث خصائصه (الـ object مشترك).
            // Server returns { token, userId, name, phone, role } — انظر AuthController.VerifyOtp
            _app.Auth.UserId      = Guid.TryParse(env.Data.UserId, out var g) ? g : null;
            _app.Auth.FullName    = env.Data.Name;
            _app.Auth.Phone       = phone;
            _app.Auth.AccessToken = env.Data.Token;
            _app.NotifyChanged();
        }
        finally { IsBusy = false; FireChanged(); }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        IsBusy = true; FireChanged();
        try
        {
            await _api.PostAsync<object>("/auth/logout", null, ct);
            _app.Auth.UserId      = null;
            _app.Auth.FullName    = null;
            _app.Auth.Phone       = null;
            _app.Auth.AccessToken = null;
            _app.NotifyChanged();
        }
        finally { IsBusy = false; FireChanged(); }
    }

    private void FireChanged() => Changed?.Invoke();
    public void Dispose() => _app.OnChanged -= FireChanged;

    /// <summary>Server response shape — see AuthController.VerifyOtp.</summary>
    private sealed record AuthResponse(string Token, string UserId, string Name, string Phone, string Role);
}
