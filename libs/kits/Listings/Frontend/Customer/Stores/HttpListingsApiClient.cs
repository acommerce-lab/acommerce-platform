using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.Kits.Listings.Domain;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

/// <summary>
/// التَنفيذ الافتراضيّ — يَعرف الـ wire shape بالضبط:
/// <list type="bullet">
///   <item><c>GET /listings</c> ⇒ <c>OperationEnvelope&lt;{ total, page, pageSize, items: ListingDto[] }&gt;</c></item>
///   <item><c>GET /listings/{id}</c> ⇒ <c>OperationEnvelope&lt;ListingDto&gt;</c> (مع enricher fields)</item>
///   <item><c>GET /my-listings</c> ⇒ <c>OperationEnvelope&lt;ListingDto[]&gt;</c></item>
/// </list>
///
/// <para>التَطبيق يَحقن <c>HttpClient</c> مَوصول لـ <c>EjarApi:BaseUrl</c>
/// مع AuthHeadersHandler. هذا الـ client يُقَشِّر الـ envelope ويَردّ
/// <see cref="IListing"/>؛ الـ widgets لا تَرى JSON إطلاقاً.</para>
///
/// <para>تَطبيق آخر بـ wire shape مختلفة (مثلاً GraphQL أو REST بدون OAM)
/// يُسَجِّل تنفيذاً خاصّاً لـ <see cref="IListingsApiClient"/> — kit pages
/// لا تَتأثّر.</para>
/// </summary>
public sealed class HttpListingsApiClient : IListingsApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public HttpListingsApiClient(HttpClient http) => _http = http;

    public async Task<ListingPageResult> SearchAsync(ListingFilter filter, CancellationToken ct = default)
    {
        var qs = BuildQuery(filter);
        try
        {
            var env = await _http.GetFromJsonAsync<OperationEnvelope<ListingPageDto>>(
                "/listings" + qs, _json, ct);
            if (env?.Operation.Status == "Success" && env.Data?.Items is { } items)
                return new ListingPageResult(items.Cast<IListing>().ToList(),
                                              env.Data.Total, env.Data.Page, env.Data.PageSize);
        }
        catch { /* network/shape — empty page */ }
        return new ListingPageResult(Array.Empty<IListing>(), 0, filter.Page, filter.PageSize);
    }

    public async Task<IListing?> GetAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var env = await _http.GetFromJsonAsync<OperationEnvelope<ListingDto>>(
                $"/listings/{Uri.EscapeDataString(id)}", _json, ct);
            return env?.Operation.Status == "Success" ? env.Data : null;
        }
        catch { return null; }
    }

    public async Task<IReadOnlyList<IListing>> ListMineAsync(CancellationToken ct = default)
    {
        try
        {
            var env = await _http.GetFromJsonAsync<OperationEnvelope<List<ListingDto>>>(
                "/my-listings", _json, ct);
            if (env?.Operation.Status == "Success" && env.Data is { } list)
                return list.Cast<IListing>().ToList();
        }
        catch { /* network/shape */ }
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
