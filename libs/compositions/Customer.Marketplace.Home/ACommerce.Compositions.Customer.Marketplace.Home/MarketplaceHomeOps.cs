using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Compositions.Customer.Marketplace.Home;

/// <summary>
/// مَصنَع عَمَليّات composition Marketplace Home عَلى الـ client. كلّ مَكالَمة
/// قِراءَة تُمَثَّل بِقَيد محاسبيّ مُنفَصِل: From (browser) → To (marketplace
/// server). Tags بِبادِئَة <c>qs.</c> تَذهَب لِـ query string تلقائيّاً
/// (HttpDispatcher).
/// </summary>
public static class MarketplaceHomeOps
{
    public static Operation HomeView(string? city)
    {
        var b = Entry.Create("home.view")
            .From("User:current",         1, ("role", "browser"))
            .To("Server:marketplace",     1, ("role", "home"));
        if (!string.IsNullOrWhiteSpace(city)) b.Tag("qs.city", city);
        return b.Build();
    }

    public static Operation HomeExplore(ExploreFilter f)
    {
        var b = Entry.Create("home.explore")
            .From("User:current",         1, ("role", "browser"))
            .To("Server:marketplace",     1, ("role", "explore"));
        if (!string.IsNullOrWhiteSpace(f.City))         b.Tag("qs.city", f.City);
        if (!string.IsNullOrWhiteSpace(f.PropertyType)) b.Tag("qs.propertyType", f.PropertyType);
        if (!string.IsNullOrWhiteSpace(f.Query))        b.Tag("qs.q", f.Query);
        if (f.MinBedrooms > 0)                          b.Tag("qs.minBedrooms", f.MinBedrooms.ToString());
        if (f.MinPrice is not null)                     b.Tag("qs.minPrice", f.MinPrice.Value.ToString());
        if (f.MaxPrice is not null)                     b.Tag("qs.maxPrice", f.MaxPrice.Value.ToString());
        if (!string.IsNullOrWhiteSpace(f.Sort))         b.Tag("qs.sort", f.Sort);
        return b.Build();
    }

    public static Operation HomeSearchSuggestions() => Entry
        .Create("home.search.suggestions")
        .From("User:current",         1, ("role", "browser"))
        .To("Server:marketplace",     1, ("role", "suggestions"))
        .Build();

    public static Operation LegalList() => Entry
        .Create("legal.list")
        .From("User:current",         1, ("role", "browser"))
        .To("Server:marketplace",     1, ("role", "legal"))
        .Build();
}
