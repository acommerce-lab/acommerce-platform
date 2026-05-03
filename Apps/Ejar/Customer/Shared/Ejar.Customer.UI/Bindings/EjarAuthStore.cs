using ACommerce.Kits.Auth.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IAuthStore"/> لإيجار. يَلفّ AppStore الموجود الذي يَحوي
/// JWT + user info. النسخة الأوّليّة تُخزّن state داخليّاً — التَكامل الكامل
/// مع <c>EjarAuthenticationStateProvider</c> يأتي في pass لاحق.
/// </summary>
public sealed class EjarAuthStore : IAuthStore
{
    public bool IsAuthenticated { get; private set; }
    public string? UserId { get; private set; }
    public string? FullName { get; private set; }
    public bool IsBusy { get; private set; }
    public string? LastError { get; private set; }
    public event Action? Changed;

    public Task RequestOtpAsync(string phone, CancellationToken ct = default)
    {
        // TODO: dispatch auth.sms.request عبر ClientOpEngine، وحدّث IsBusy/LastError
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task VerifyOtpAsync(string phone, string code, CancellationToken ct = default)
    {
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        IsAuthenticated = false;
        UserId = null;
        FullName = null;
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
