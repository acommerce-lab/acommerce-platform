using ACommerce.Kits.Discovery.Domain;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Mvc;

namespace ACommerce.Compositions.Customer.Marketplace.Home.Backend;

/// <summary>
/// كُنترولَر <c>/home/*</c> + <c>/legal</c> مُوَحَّد. يَستَخدِم الـ ports:
/// <see cref="IHomeListingsSource"/> (يَجلِب)، <see cref="IHomeListingProjection"/>
/// (يُحَوِّل)، <see cref="IHomeSearchSuggestions"/> (popular)،
/// <see cref="ILegalPageProvider"/> (legal).
///
/// <para>يَستَبدِل <c>HomeController</c> القَديم في كُلّ تَطبيق Marketplace.
/// إيجار + عشير V3 لَم يَعودا يَنسَخان نَفس الـ ٤ endpoints — هُنا واحِد فَقَط
/// مُغَطّى بِـ <see cref="MarketplaceHomeBackendCompositionExtensions"/>.</para>
/// </summary>
[ApiController]
public sealed class MarketplaceHomeController : ControllerBase
{
    private readonly IHomeListingsSource    _src;
    private readonly IHomeListingProjection _projection;
    private readonly IHomeSearchSuggestions _suggestions;
    private readonly ILegalPageProvider     _legal;
    private readonly OpEngine               _engine;

    public MarketplaceHomeController(
        IHomeListingsSource src,
        IHomeListingProjection projection,
        IHomeSearchSuggestions suggestions,
        ILegalPageProvider legal,
        OpEngine engine)
    {
        _src         = src;
        _projection  = projection;
        _suggestions = suggestions;
        _legal       = legal;
        _engine      = engine;
    }

    /// <summary>
    /// <c>GET /home/view</c> — الصَفحَة الرَئيسِيَّة: categories + featured + new.
    /// <c>featured</c> = الإعلانات الَّتي <c>IsVerified == true</c>، <c>new</c> =
    /// أَوَّل ٦ مَن الباقي بِتَرتيب الـ source. الـ city يَفلِتِر كِليهما.
    /// </summary>
    [HttpGet("/home/view")]
    public async Task<IActionResult> HomeView(
        [FromQuery] string? city,
        [FromServices] IDiscoveryCategoryProvider categoryProvider,
        CancellationToken ct)
    {
        var listings   = await _src.GetActiveListingsAsync(city, ct);
        var categories = await categoryProvider.GetCategoriesAsync(ct);

        var featured = listings.Where(l => l.IsVerified)
                               .Select(l => _projection.MapCard(l, categories)).ToList();
        var newest   = listings.Where(l => !l.IsVerified).Take(6)
                               .Select(l => _projection.MapCard(l, categories)).ToList();

        return this.OkEnvelope("home.view", new
        {
            categories = categories.Select(c => new { id = c.Slug, label = c.Label, icon = c.Icon }),
            featured,
            @new = newest,
            city,
        });
    }

    /// <summary>
    /// <c>GET /home/explore</c> — قائِمَة Explore المَسطَّحَة. كُلّ مَعَلَمات
    /// الـ query تَنزَلِق إلى <see cref="ExploreFilter"/> ثُمّ إلى
    /// <see cref="IHomeListingsSource.ExploreAsync"/>.
    /// </summary>
    [HttpGet("/home/explore")]
    public async Task<IActionResult> Explore(
        [FromQuery] string? city,
        [FromQuery] string? category,        // alias لِـ propertyType (V1 compat)
        [FromQuery] string? propertyType,
        [FromQuery] string? q,
        [FromQuery] int minBedrooms = 0,
        [FromQuery(Name = "minPrice")] decimal? minPrice = null,
        [FromQuery(Name = "maxPrice")] decimal? maxPrice = null,
        [FromQuery] string? sort = null,
        [FromServices] IDiscoveryCategoryProvider? categoryProvider = null,
        CancellationToken ct = default)
    {
        var filter = new ExploreFilter(
            City:         string.IsNullOrWhiteSpace(city) ? null : city,
            PropertyType: propertyType ?? category,
            Query:        string.IsNullOrWhiteSpace(q) ? null : q,
            MinPrice:     minPrice,
            MaxPrice:     maxPrice,
            MinBedrooms:  minBedrooms,
            Sort:         sort);

        var listings   = await _src.ExploreAsync(filter, ct);
        var categories = categoryProvider is null
            ? Array.Empty<DiscoveryCategory>()
            : await categoryProvider.GetCategoriesAsync(ct);

        var items = listings.Select(l => _projection.MapCard(l, categories)).ToList();
        return this.OkEnvelope("home.explore", items);
    }

    /// <summary>
    /// <c>GET /home/search/suggestions</c> — popular + recent. كَلِمات popular
    /// تَأتي مَن <see cref="IHomeSearchSuggestions"/> (التَطبيق يُسَجِّل
    /// قائِمَة سُوقه)، recent فارِغَة افتِراضيّاً (لا history persistence
    /// في هذا الـ composition).
    /// </summary>
    [HttpGet("/home/search/suggestions")]
    public IActionResult Suggestions() =>
        this.OkEnvelope("home.search.suggestions", new
        {
            recent  = _suggestions.Recent,
            popular = _suggestions.Popular,
        });

    /// <summary><c>GET /legal</c> — قائِمَة الصَفحات القانونِيَّة.</summary>
    [HttpGet("/legal")]
    public IActionResult Legal() =>
        this.OkEnvelope("legal.list", _legal.List.Select(p => new { key = p.Key, label = p.Label }));
}

/// <summary>
/// مَنفَذ صَغير يَجلِب فِئات Discovery بِشَكل cancelation-aware. الـ kit
/// المَوجود يَكشِف <c>DiscoveryCategory</c> عَبر <c>OpEngine</c>؛ هذا
/// wrapper يَجعَل الكُنترولَر مُستَقِلّاً عَن تَفاصيل DataInterceptor.
/// التَطبيق يُنَفِّذه بِسَطر واحِد فَوق DbContext.
/// </summary>
public interface IDiscoveryCategoryProvider
{
    Task<IReadOnlyList<DiscoveryCategory>> GetCategoriesAsync(CancellationToken ct);
}
