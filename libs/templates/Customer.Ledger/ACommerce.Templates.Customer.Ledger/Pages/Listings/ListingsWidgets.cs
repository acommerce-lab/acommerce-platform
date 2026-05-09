using ACommerce.Kits.Listings.Frontend.Customer.Widgets;

namespace ACommerce.Kits.Listings.Frontend.Customer;

/// <summary>widgets الـ Listings المتاحة للتطبيق.</summary>
public static class ListingsWidgets
{
    public static Type Explore       => typeof(AcListingExploreWidget);
    public static Type Details       => typeof(AcListingDetailsWidget);
    public static Type Mine          => typeof(AcMyListingsWidget);
    public static Type Create        => typeof(AcCreateListingWidget);
}
