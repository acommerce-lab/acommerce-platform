using ACommerce.ClientHost.KitApi;
using ACommerce.Kits.Listings.Domain;

namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

/// <summary>
/// التَنفيذ الافتراضيّ — يَستهلك <see cref="KitHttpClient"/> الموحَّد فيَستفيد
/// تلقائيّاً من كلّ analyzers/interceptors المُسَجَّلة في التطبيق (auth،
/// telemetry، retry، …) دون تَكرار. هو يَعرف فقط wire shapes:
/// <list type="bullet">
///   <item><c>GET /listings</c> ⇒ <c>{ total, page, pageSize, items: ListingDto[] }</c></item>
///   <item><c>GET /listings/{id}</c> ⇒ <c>ListingDto</c></item>
///   <item><c>GET /my-listings</c> ⇒ <c>ListingDto[]</c></item>
/// </list>
/// تَقشير <c>OperationEnvelope</c> يَتمّ داخل <c>KitHttpClient.PeelEnvelope</c>.
///
/// <para>تَطبيق آخر بـ wire shape مختلفة (GraphQL مثلاً) يُسَجِّل تنفيذاً
/// خاصّاً لـ <see cref="IListingsApiClient"/> — kit pages لا تَتأثّر.</para>
/// </summary>
public sealed class HttpListingsApiClient : IListingsApiClient
{
    private const string Kit = "listings";
    private readonly KitHttpClient _http;

    public HttpListingsApiClient(KitHttpClient http) => _http = http;

    public async Task<ListingPageResult> SearchAsync(ListingFilter filter, CancellationToken ct = default)
    {
        var qs = BuildQuery(filter);
        var res = await _http.GetAsync<ListingPageDto>(Kit, "/listings" + qs, ct);
        if (res.Success && res.Data?.Items is { } items)
            return new ListingPageResult(items.Cast<IListing>().ToList(),
                                          res.Data.Total, res.Data.Page, res.Data.PageSize);
        return new ListingPageResult(Array.Empty<IListing>(), 0, filter.Page, filter.PageSize);
    }

    public async Task<IListing?> GetAsync(string id, CancellationToken ct = default)
    {
        var res = await _http.GetAsync<ListingDto>(Kit, $"/listings/{Uri.EscapeDataString(id)}", ct);
        return res.Success ? res.Data : null;
    }

    public async Task<IReadOnlyList<IListing>> ListMineAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync<List<ListingDto>>(Kit, "/my-listings", ct);
        if (res.Success && res.Data is { } list)
            return list.Cast<IListing>().ToList();
        return Array.Empty<IListing>();
    }

    private static string BuildQuery(ListingFilter f)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.City))         parts.Add($"city={Uri.EscapeDataString(f.City)}");
        if (!string.IsNullOrWhiteSpace(f.PropertyType)) parts.Add($"propertyType={Uri.EscapeDataString(f.PropertyType)}");
        if (f.PriceMin is { } min)                      parts.Add($"priceMin={min}");
        if (f.PriceMax is { } max)                      parts.Add($"priceMax={max}");
        if (f.BedroomsMin is { } b)                     parts.Add($"minBedrooms={b}");
        if (!string.IsNullOrWhiteSpace(f.Query))        parts.Add($"q={Uri.EscapeDataString(f.Query)}");
        parts.Add($"page={f.Page}");
        parts.Add($"pageSize={f.PageSize}");
        return "?" + string.Join("&", parts);
    }

    /// <summary>Wire shape لـ /listings — مُحاذٍ لـ ListingsController.Search.</summary>
    private sealed record ListingPageDto(int Total, int Page, int PageSize, List<ListingDto>? Items);

    /// <summary>
    /// DTO يُحقّق <see cref="IListing"/> (Law 6). AreaSqm كـ double لاستيعاب
    /// أرقام عشريّة من JSON، ثمّ explicit interface implementation يَردّ int.
    /// </summary>
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
        public double  AreaSqm       { get; set; }
        int IListing.AreaSqm        => (int)Math.Round(AreaSqm);
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
