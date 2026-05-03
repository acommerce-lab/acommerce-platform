using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Listings.Frontend.Customer.Stores;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IListingsStore"/> لإيجار. يَجلب من <c>GET /listings</c>
/// مع query string من <see cref="ListingFilter"/>، و<c>GET /my-listings</c>
/// لمتجر "إعلاناتي". يَكشف <see cref="IListing"/> فقط — التَطبيق يَستطيع
/// تَبديل شكل DTO الخادميّ دون كسر صفحات الكيت.
/// </summary>
public sealed class EjarListingsStore : IListingsStore
{
    private readonly ApiReader _api;
    private List<IListing> _visible = new();
    private List<IListing> _mine    = new();

    public EjarListingsStore(ApiReader api) => _api = api;

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
            var qs = BuildQuery(filter);
            var env = await _api.GetAsync<List<ListingDto>>("/listings" + qs, localize: true, ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _visible = env.Data.Cast<IListing>().ToList();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task<IListing?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var env = await _api.GetAsync<ListingDto>(
            $"/listings/{Uri.EscapeDataString(id)}", localize: true, ct: ct);
        return env.Operation.Status == "Success" ? env.Data : null;
    }

    public async Task LoadMineAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _api.GetAsync<List<ListingDto>>("/my-listings", localize: true, ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _mine = env.Data.Cast<IListing>().ToList();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    private static string BuildQuery(ListingFilter f)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.City))         parts.Add($"city={Uri.EscapeDataString(f.City)}");
        if (!string.IsNullOrWhiteSpace(f.PropertyType)) parts.Add($"type={Uri.EscapeDataString(f.PropertyType)}");
        if (f.PriceMin is { } min)                      parts.Add($"priceMin={min}");
        if (f.PriceMax is { } max)                      parts.Add($"priceMax={max}");
        if (f.BedroomsMin is { } b)                     parts.Add($"bedroomsMin={b}");
        if (!string.IsNullOrWhiteSpace(f.Query))        parts.Add($"q={Uri.EscapeDataString(f.Query)}");
        parts.Add($"page={f.Page}");
        parts.Add($"pageSize={f.PageSize}");
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    /// <summary>DTO خادميّ يُحقّق <see cref="IListing"/> مباشرةً (Law 6).</summary>
    private sealed class ListingDto : IListing
    {
        public string  Id            { get; set; } = "";
        public string  OwnerId       { get; set; } = "";
        public string  Title         { get; set; } = "";
        public string  Description   { get; set; } = "";
        public decimal Price         { get; set; }
        public string  TimeUnit      { get; set; } = "monthly";
        public string  PropertyType  { get; set; } = "";
        public string  City          { get; set; } = "";
        public string  District      { get; set; } = "";
        public double  Lat           { get; set; }
        public double  Lng           { get; set; }
        public int     BedroomCount  { get; set; }
        public int     BathroomCount { get; set; }
        public int     AreaSqm       { get; set; }
        public int     Status        { get; set; }
        public int     ViewsCount    { get; set; }
        public bool    IsVerified    { get; set; }
        public string? ThumbnailUrl  { get; set; }
        public IReadOnlyList<string> Images    { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Amenities { get; set; } = Array.Empty<string>();
        public DateTime  CreatedAt   { get; set; }
        public DateTime? UpdatedAt   { get; set; }
    }
}
