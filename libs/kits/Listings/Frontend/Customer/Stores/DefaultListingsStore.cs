using ACommerce.Kits.Listings.Domain;

namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتراضيّ لـ <see cref="IListingsStore"/> يَدلّع لـ
/// <see cref="IListingsApiClient"/>. التَطبيقات لا تَحتاج كتابة Binding
/// إلا لو احتاجت كاش، optimistic update، أو مَصدر بَيانات إضافيّ.
/// </summary>
public sealed class DefaultListingsStore : IListingsStore
{
    private readonly IListingsApiClient _api;
    private List<IListing> _visible = new();
    private List<IListing> _mine    = new();

    public DefaultListingsStore(IListingsApiClient api) => _api = api;

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
