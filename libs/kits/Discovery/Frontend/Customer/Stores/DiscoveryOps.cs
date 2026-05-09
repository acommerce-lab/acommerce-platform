using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Discovery.Frontend.Customer.Stores;

/// <summary>
/// مَصنَع عَمَليّات Discovery kit عَلى جانِب العَميل. كلّ مَكالَمة قِراءَة
/// كاتالوج تُمَثَّل بِقَيد محاسبيّ مُنفَصِل: From (browser) → To (catalog server)،
/// مَع tags تُحَدِّد أيّ كاتالوج (cities / amenities / categories).
/// </summary>
public static class DiscoveryOps
{
    public static Operation ListCities() => Entry
        .Create("discovery.cities.list")
        .From("User:current",     1, ("role", "browser"))
        .To("Server:discovery",   1, ("role", "catalog"))
        .Tag("kind", "cities")
        .Build();

    public static Operation ListAmenities() => Entry
        .Create("discovery.amenities.list")
        .From("User:current",     1, ("role", "browser"))
        .To("Server:discovery",   1, ("role", "catalog"))
        .Tag("kind", "amenities")
        .Build();

    public static Operation ListCategories() => Entry
        .Create("discovery.categories.list")
        .From("User:current",     1, ("role", "browser"))
        .To("Server:discovery",   1, ("role", "catalog"))
        .Tag("kind", "categories")
        .Build();
}
