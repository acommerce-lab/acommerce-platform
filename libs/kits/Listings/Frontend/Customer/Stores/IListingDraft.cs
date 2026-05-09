namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

/// <summary>
/// مُسَوَّدَة إعلان جَديد — حالة UI مَحَلّيّة لِصَفحَة <c>CreateListing</c>
/// تَعيش بَين خُطوات المعالِج (Wizard) وتَنجو مِن re-render. لا HTTP، لا
/// persistence افتراضيّاً (V1 لا يُخَزِّنها). تَطبيقات أَرادَت تَزامُن
/// localStorage تُغَلِّفها بـ persistence wrapper.
///
/// <para>الـ submit يَتَحَوَّل إلى <see cref="ListingDraftPayload"/> ويُمَرَّر
/// لِـ <c>IListingsStore.CreateAsync</c> — هي لا تَعرِف عَن HTTP أَو OAM.</para>
/// </summary>
public interface IListingDraft
{
    string?  Title { get; set; }
    string?  Description { get; set; }
    decimal  Price { get; set; }
    /// <summary>"daily" | "monthly" | "yearly". افتراضيّاً "monthly".</summary>
    string   TimeUnit { get; set; }
    /// <summary>slug مِن <c>IDiscoveryStore.Categories</c>.</summary>
    string?  CategoryId { get; set; }
    string?  City { get; set; }
    string?  District { get; set; }
    int      BedroomCount { get; set; }
    /// <summary>slugs مِن <c>IDiscoveryStore.Amenities</c>. قابِلَة لِلتَعديل في الواجِهَة.</summary>
    ICollection<string> Amenities { get; }

    event Action? Changed;
    void NotifyChanged();
    void Clear();
}

/// <summary>تَنفيذ افتراضيّ POCO scoped لـ <see cref="IListingDraft"/>.</summary>
public sealed class DefaultListingDraft : IListingDraft
{
    public string?  Title         { get; set; }
    public string?  Description   { get; set; }
    public decimal  Price         { get; set; }
    public string   TimeUnit      { get; set; } = "monthly";
    public string?  CategoryId    { get; set; }
    public string?  City          { get; set; }
    public string?  District      { get; set; }
    public int      BedroomCount  { get; set; }
    public ICollection<string> Amenities { get; } = new HashSet<string>();

    public event Action? Changed;
    public void NotifyChanged() => Changed?.Invoke();

    public void Clear()
    {
        Title = null; Description = null; Price = 0; TimeUnit = "monthly";
        CategoryId = null; City = null; District = null;
        BedroomCount = 0; Amenities.Clear();
        NotifyChanged();
    }
}
