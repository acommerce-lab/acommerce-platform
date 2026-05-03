using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Listings.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IListingsStore"/> لإيجار. يَجلب IListing عبر
/// <c>/listings</c> + <c>/my-listings</c> ويَحفظ كاش محلّيّ. الـ store
/// يُسجَّل Scoped — يَعيش طوال الجلسة.
/// </summary>
public sealed class EjarListingsStore : IListingsStore
{
    private List<IListing> _visible = new();
    private List<IListing> _mine = new();

    public IReadOnlyList<IListing> Visible => _visible;
    public IReadOnlyList<IListing> Mine    => _mine;
    public bool IsLoading { get; private set; }
    public ListingFilter Filter { get; private set; } = ListingFilter.Empty;
    public event Action? Changed;

    public Task ApplyFilterAsync(ListingFilter filter, CancellationToken ct = default)
    {
        Filter = filter;
        // TODO: GET /listings?filter=… ⇒ _visible = response
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task<IListing?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        // TODO: GET /listings/{id} ⇒ IListing?
        return Task.FromResult<IListing?>(null);
    }

    public Task LoadMineAsync(CancellationToken ct = default)
    {
        // TODO: GET /my-listings ⇒ _mine = response
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
