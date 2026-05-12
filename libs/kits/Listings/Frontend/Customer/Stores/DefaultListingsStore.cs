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

    public async Task<IListing?> CreateAsync(ListingDraftPayload payload, CancellationToken ct = default)
    {
        var body = new
        {
            title         = payload.Title,
            description   = payload.Description,
            price         = payload.Price,
            timeUnit      = payload.TimeUnit,
            propertyType  = payload.PropertyType,
            city          = payload.City,
            district      = payload.District,
            lat           = payload.Lat,
            lng           = payload.Lng,
            bedroomCount  = payload.BedroomCount,
            bathroomCount = payload.BathroomCount,
            areaSqm       = payload.AreaSqm,
            amenities     = payload.Amenities,
            images        = payload.Images,
            thumbnail     = payload.Thumbnail,
            attributes    = payload.Attributes,
        };

        var env = await _engine.ExecuteAsync<InMemoryListing>(
            ListingsOps.Create(payload), payload: body, ct: ct);

        if (env.Operation.Status != "Success" || env.Data is null) return null;

        _mine = _mine.Concat(new IListing[] { env.Data }).ToList();
        Changed?.Invoke();
        return env.Data;
    }

    public async Task ToggleStatusAsync(string id, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<ToggleResultDto>(ListingsOps.ToggleStatus(id), ct: ct);
        if (env.Operation.Status != "Success" || env.Data is null) return;

        var idx = _mine.FindIndex(l => l.Id == id);
        if (idx < 0) return;

        // الـ status يَأتي مِن السيرفر — نَستَبدِل القَيد كاملاً بِنُسخَة جَديدة
        // مِن InMemoryListing بِنَفس البيانات + الحالة الجَديدة. لا نُعدِّل
        // IListing مُباشَرَةً (فالقُيود غير قابِلَة لِلتَعديل).
        var l = _mine[idx];
        var updated = new InMemoryListing(
            l.Id, l.OwnerId, l.Title, l.Description, l.Price, l.TimeUnit,
            l.PropertyType, l.City, l.District, l.Lat, l.Lng,
            l.BedroomCount, l.BathroomCount, l.AreaSqm,
            env.Data.Status, l.ViewsCount, l.IsVerified,
            l.ThumbnailUrl, l.Images, l.Amenities, l.CreatedAt, l.UpdatedAt);
        _mine = _mine.Take(idx).Append((IListing)updated).Concat(_mine.Skip(idx + 1)).ToList();
        Changed?.Invoke();
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<object>(ListingsOps.Delete(id), ct: ct);
        if (env.Operation.Status != "Success") return;
        _mine = _mine.Where(l => l.Id != id).ToList();
        Changed?.Invoke();
    }

    private sealed class ListingPageDto
    {
        public List<InMemoryListing>? Items { get; set; }
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Wire shape لِـ <c>POST /my-listings/{id}/toggle</c>: <c>{ id, status }</c>.</summary>
    private sealed class ToggleResultDto
    {
        public string Id { get; set; } = "";
        public int Status { get; set; }
    }
}
