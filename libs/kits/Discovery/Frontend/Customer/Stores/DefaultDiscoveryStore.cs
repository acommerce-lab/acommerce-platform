using ACommerce.Client.Operations;

namespace ACommerce.Kits.Discovery.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتراضيّ لِـ <see cref="IDiscoveryStore"/> — OAM-shaped. كلّ
/// مَكالَمة قِراءَة تُرسَل عَبر <see cref="ITemplateEngine"/> فتَستَفيد مِن
/// كلّ interceptors المُسَجَّلة (telemetry، culture localization، retry).
/// </summary>
public sealed class DefaultDiscoveryStore : IDiscoveryStore
{
    private readonly ITemplateEngine _engine;
    private List<string> _cities = new();
    private List<DiscoveryAmenityItem> _amenities = new();
    private List<DiscoveryCategoryItem> _categories = new();

    public DefaultDiscoveryStore(ITemplateEngine engine) => _engine = engine;

    public IReadOnlyList<string> Cities => _cities;
    public IReadOnlyList<DiscoveryAmenityItem> Amenities => _amenities;
    public IReadOnlyList<DiscoveryCategoryItem> Categories => _categories;
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            await Task.WhenAll(
                LoadCitiesAsync(ct),
                LoadAmenitiesAsync(ct),
                LoadCategoriesAsync(ct));
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task LoadCitiesAsync(CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<List<string>>(DiscoveryOps.ListCities(), ct: ct);
        if (env.Operation.Status == "Success" && env.Data is not null)
        {
            _cities = env.Data;
            Changed?.Invoke();
        }
    }

    public async Task LoadAmenitiesAsync(CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<List<DiscoveryAmenityItem>>(
            DiscoveryOps.ListAmenities(), ct: ct);
        if (env.Operation.Status == "Success" && env.Data is not null)
        {
            _amenities = env.Data;
            Changed?.Invoke();
        }
    }

    public async Task LoadCategoriesAsync(CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<List<DiscoveryCategoryItem>>(
            DiscoveryOps.ListCategories(), ct: ct);
        if (env.Operation.Status == "Success" && env.Data is not null)
        {
            _categories = env.Data;
            Changed?.Invoke();
        }
    }
}
