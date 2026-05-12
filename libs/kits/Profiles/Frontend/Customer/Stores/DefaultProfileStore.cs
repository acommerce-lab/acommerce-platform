using ACommerce.Client.Operations;
using ACommerce.ClientHost.Auth;
using ACommerce.Kits.Profiles.Operations;

namespace ACommerce.Kits.Profiles.Frontend.Customer.Stores;

/// <summary>OAM-shaped (F61) — profile.get_mine / profile.update عَبر ITemplateEngine.
/// يَستَجيب لِتَغيُّر <see cref="IClientAuthState"/>: عِند logout يَمسَح <c>Current</c>،
/// عِند login يَتَرُك الـ page تَستَدعي <c>LoadAsync</c> صَراحَةً (الـ store
/// لا يَجلِب تِلقائيّاً لِيَتَجَنَّب طَلَب HTTP زائِد عَلى تَدَفُّقات
/// background).</summary>
public sealed class DefaultProfileStore : IProfileStore, IDisposable
{
    private readonly ITemplateEngine _engine;
    private readonly IClientAuthState? _auth;
    public DefaultProfileStore(ITemplateEngine engine, IClientAuthState? auth = null)
    {
        _engine = engine;
        _auth = auth;
        if (_auth is not null) _auth.OnChanged += OnAuthChanged;
    }

    public IUserProfile? Current { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<InMemoryUserProfile>(ProfilesOps.GetMine(), ct: ct);
            // اِمسَح الـ Current أَوَّلاً ثُمّ ضَع لَو نَجَح — يُجَنِّب
            // عَرض بَيانات مُستَخدِم سابِق بَعد logout/login.
            Current = (env.Operation.Status == "Success" && env.Data is not null)
                ? env.Data
                : null;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task UpdateAsync(IUserProfile next, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<InMemoryUserProfile>(
            ProfilesOps.Update(), payload: next, ct: ct);
        if (env.Operation.Status == "Success" && env.Data is not null)
            Current = env.Data;
        Changed?.Invoke();
    }

    private void OnAuthChanged()
    {
        if (_auth?.IsAuthenticated == false && Current is not null)
        {
            Current = null;
            Changed?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_auth is not null) _auth.OnChanged -= OnAuthChanged;
    }
}
