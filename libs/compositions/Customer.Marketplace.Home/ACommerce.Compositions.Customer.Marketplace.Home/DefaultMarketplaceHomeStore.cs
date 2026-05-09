using ACommerce.Client.Operations;

namespace ACommerce.Compositions.Customer.Marketplace.Home;

/// <summary>
/// تَنفيذ افتراضيّ لِـ <see cref="IMarketplaceHomeStore"/> — pure-OAM. كلّ
/// مَكالَمة تَخرُج عَبر <see cref="ITemplateEngine"/> فتَستَفيد مِن
/// interceptors المُسَجَّلَة (culture localization، telemetry، retry-on-401).
/// </summary>
public sealed class DefaultMarketplaceHomeStore : IMarketplaceHomeStore
{
    private readonly ITemplateEngine _engine;
    private List<HomeListingCard> _explore = new();

    public DefaultMarketplaceHomeStore(ITemplateEngine engine) => _engine = engine;

    public HomeView? Home { get; private set; }
    public IReadOnlyList<HomeListingCard> Explore => _explore;
    public IReadOnlyList<LegalDoc>? LegalDocs { get; private set; }
    public SearchSuggestions? Suggestions { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadHomeAsync(string? city = null, CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<HomeViewDto>(MarketplaceHomeOps.HomeView(city), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is { } d)
            {
                var cats = d.Categories?.Select(c => new HomeCategoryItem(c.Id, c.Label, c.Icon)).ToList()
                          ?? new List<HomeCategoryItem>();
                var feat = d.Featured?.Select(MapCard).ToList() ?? new List<HomeListingCard>();
                var nev  = d.New?.Select(MapCard).ToList() ?? new List<HomeListingCard>();
                Home = new HomeView(cats, feat, nev, d.City);
            }
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task ApplyExploreAsync(ExploreFilter filter, CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<List<HomeListingCardDto>>(
                MarketplaceHomeOps.HomeExplore(filter), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _explore = env.Data.Select(MapCard).ToList();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task LoadSuggestionsAsync(CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<SearchSuggestionsDto>(
            MarketplaceHomeOps.HomeSearchSuggestions(), ct: ct);
        if (env.Operation.Status == "Success" && env.Data is { } d)
        {
            Suggestions = new SearchSuggestions(
                (IReadOnlyList<string>?)d.Recent  ?? Array.Empty<string>(),
                (IReadOnlyList<string>?)d.Popular ?? Array.Empty<string>());
            Changed?.Invoke();
        }
    }

    public async Task LoadLegalAsync(CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<List<LegalDocDto>>(MarketplaceHomeOps.LegalList(), ct: ct);
        if (env.Operation.Status == "Success" && env.Data is not null)
        {
            LegalDocs = env.Data.Select(d => new LegalDoc(d.Key, d.Label)).ToList();
            Changed?.Invoke();
        }
    }

    private static HomeListingCard MapCard(HomeListingCardDto d) => new(
        d.Id ?? "", d.Title ?? "", d.Price, d.TimeUnit, d.TimeUnitLabel,
        d.PropertyType, d.PropertyTypeLabel, d.City, d.District, d.Lat, d.Lng,
        d.BedroomCount, d.AreaSqm, d.IsVerified, d.ViewsCount,
        d.IsFavorite,
        (IReadOnlyList<string>?)d.Amenities ?? Array.Empty<string>(),
        d.FirstImage);

    // ── Wire DTOs ─────────────────────────────────────────────────────────
    // مُحاذيَة لِشَكل ردّ الـ backend (HomeController.cs). System.Text.Json
    // يَفُكّ camelCase تلقائيّاً عَبر JsonSerializerDefaults.Web (مَفعَّل في
    // كلّ مَكان عَبر ApiReader/HttpDispatcher).

    private sealed class HomeViewDto
    {
        public List<HomeCategoryDto>?    Categories { get; set; }
        public List<HomeListingCardDto>? Featured   { get; set; }
        // ‎"new" كَلِمَة مُحجوزَة — JsonPropertyName لَيس مُتوَفِّراً هنا، نَستَخدِم Property
        // مُسَمّاة New (System.Text.Json يُطَبِّق camelCase ⇒ "new" يُربَط بِـ New).
        public List<HomeListingCardDto>? New        { get; set; }
        public string?                   City       { get; set; }
    }

    private sealed class HomeCategoryDto
    {
        public string  Id    { get; set; } = "";
        public string  Label { get; set; } = "";
        public string? Icon  { get; set; }
    }

    private sealed class HomeListingCardDto
    {
        public string?  Id { get; set; }
        public string?  Title { get; set; }
        public decimal  Price { get; set; }
        public string?  TimeUnit { get; set; }
        public string?  TimeUnitLabel { get; set; }
        public string?  PropertyType { get; set; }
        public string?  PropertyTypeLabel { get; set; }
        public string?  City { get; set; }
        public string?  District { get; set; }
        public double?  Lat { get; set; }
        public double?  Lng { get; set; }
        public int      BedroomCount { get; set; }
        public int      AreaSqm { get; set; }
        public bool     IsVerified { get; set; }
        public int      ViewsCount { get; set; }
        public bool     IsFavorite { get; set; }
        public List<string>? Amenities { get; set; }
        public string?  FirstImage { get; set; }
    }

    private sealed class SearchSuggestionsDto
    {
        public List<string>? Recent  { get; set; }
        public List<string>? Popular { get; set; }
    }

    private sealed class LegalDocDto
    {
        public string Key   { get; set; } = "";
        public string Label { get; set; } = "";
    }
}
