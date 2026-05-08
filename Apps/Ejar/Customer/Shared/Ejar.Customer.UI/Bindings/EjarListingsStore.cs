using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Listings.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IListingsStore"/> لإيجار. يَدلّع للـ
/// <see cref="IListingsApiClient"/> الذي يَملك الكيت — هو من يَعرف شكل
/// الـ envelope ويُقَشِّره. هذا الـ store يُدير state الـ UI فقط (loading،
/// caches، Changed event) — لا shape knowledge هنا.
///
/// <para>يَتَوَلَّى الكيت الـ wire format بحكم أنّه صاحب controller الخادميّ.
/// تَطبيقات أخرى تَستبدل <c>IListingsApiClient</c> ببناء آخر إن احتاجت
/// (graphQL، REST بدون OAM، إلخ) — store + pages لا يَتأثّران.</para>
/// </summary>
public sealed class EjarListingsStore : IListingsStore
{
    private readonly IListingsApiClient _api;
    private List<IListing> _visible = new();
    private List<IListing> _mine    = new();

    public EjarListingsStore(IListingsApiClient api) => _api = api;

    public IReadOnlyList<IListing> Visible => _visible;
    public IReadOnlyList<IListing> Mine    => _mine;
    public bool IsLoading { get; private set; }
    public ListingFilter Filter { get; private set; } = ListingFilter.Empty;
    public event Action? Changed;

    public async Task ApplyFilterAsync(ListingFilter filter, CancellationToken ct = default)
    {
        Filter = filter;
        IsLoading = true; Changed?.Invoke();
        try
        {
            var page = await _api.SearchAsync(filter, ct);
            _visible = page.Items.ToList();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public Task<IListing?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _api.GetAsync(id, ct);

    public async Task LoadMineAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try   { _mine = (await _api.ListMineAsync(ct)).ToList(); }
        finally { IsLoading = false; Changed?.Invoke(); }
    }
}
