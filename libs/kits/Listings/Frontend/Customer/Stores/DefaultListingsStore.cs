using ACommerce.Client.Operations;
using ACommerce.Kits.Listings.Domain;

namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

/// <summary>OAM-shaped (F61): DefaultListingsStore يُرسِل قُيود محاسبيّة عَبر ITemplateEngine.</summary>
public sealed class DefaultListingsStore : IListingsStore
{
    private readonly ITemplateEngine _engine;
    private List<IListing> _visible = new();
    private List<IListing> _mine    = new();

    public DefaultListingsStore(ITemplateEngine engine) => _engine = engine;

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
            var env = await _engine.ExecuteAsync<ListingPageDto>(ListingsOps.Search(filter), ct: ct);
            if (env.Operation.Status == "Success" && env.Data?.Items is { } items)
                _visible = items.Cast<IListing>().ToList();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task<IListing?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<InMemoryListing>(ListingsOps.GetById(id), ct: ct);
        return env.Operation.Status == "Success" ? env.Data : null;
    }

    public async Task LoadMineAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<List<InMemoryListing>>(ListingsOps.ListMine(), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _mine = env.Data.Cast<IListing>().ToList();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    private sealed class ListingPageDto
    {
        public List<InMemoryListing>? Items { get; set; }
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
