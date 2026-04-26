using Microsoft.AspNetCore.Components;

namespace ACommerce.Templates.Customer.Commerce.Models;

// ── Commerce DTOs ─────────────────────────────────────────────────────────
// All DTOs follow the P-2 extension principle: strongly-typed core + Extra bag.

/// <summary>A category pill for <see cref="AcCategoryRow"/>.</summary>
public sealed record CategoryDto
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? Icon { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// A pin rendered on <see cref="AcMapSearchPage"/>. Vertical-agnostic —
/// the same shape serves vendors, offers, listings, service providers.
/// Consumer passes a <c>Href</c> and/or subscribes to OnSelectPin.
/// </summary>
public sealed record MapPinDto
{
    public required string Id { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    /// <summary>Emoji or single-character label shown on the pin bubble.</summary>
    public string? Emoji { get; init; }
    /// <summary>AcIcon name shown on the pin bubble (preferred over Emoji).</summary>
    public string? IconName { get; init; }
    public string? Href { get; init; }
    /// <summary>Top-right pin badge (e.g. discount %, rating).</summary>
    public string? Badge { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>A vendor mini reference used by offer/cart/order DTOs.</summary>
public sealed record VendorMiniDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? LogoEmoji { get; init; }
    public string? AvatarUrl { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public double? Rating { get; init; }
    public string? OpenHours { get; init; }
    public string? Phone { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>Offer card / product card DTO used by grids and details pages.</summary>
public sealed record OfferDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public decimal? OriginalPrice { get; init; }
    public string Currency { get; init; } = "SAR";
    public string? Emoji { get; init; }
    public string? ImageUrl { get; init; }
    public int DiscountPercent { get; init; }
    public bool IsFeatured { get; init; }
    public VendorMiniDto? Vendor { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>Line item inside a cart.</summary>
public sealed record CartLineDto
{
    public required string OfferId { get; init; }
    public required string Title { get; init; }
    public string? Emoji { get; init; }
    public string? ImageUrl { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal LineTotal => UnitPrice * Quantity;
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>Snapshot of the cart passed to <see cref="AcCartPage"/>.</summary>
public sealed record CartSnapshot
{
    public required IReadOnlyList<CartLineDto> Items { get; init; }
    public VendorMiniDto? Vendor { get; init; }
    public decimal Subtotal { get; init; }
    public string Currency { get; init; } = "SAR";
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>Checkout form draft state. Templates bind to this; the consumer mutates it.</summary>
public sealed class CheckoutDraft
{
    public string PickupType { get; set; } = "InStore"; // "InStore" | "Curbside"
    public string PaymentMethod { get; set; } = "Cash"; // "Cash" | "Card"
    public decimal? CashTendered { get; set; }
    public string? CarModel { get; set; }
    public string? CarColor { get; set; }
    public string? CarPlate { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>A single order row inside <see cref="AcOrdersListPage"/>.</summary>
public sealed record OrderRowDto
{
    public required string Id { get; init; }
    public required string OrderNumber { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = "SAR";
    public string Status { get; init; } = "";
    public string? StatusLabel { get; init; }
    public string? PickupType { get; init; }    // "InStore" | "Curbside"
    public string? PickupLabel { get; init; }
    public string? PaymentMethod { get; init; } // "Cash" | "Card"
    public string? PaymentLabel { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? VendorName { get; init; }
    public string? VendorEmoji { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>Full order details shown in <see cref="AcOrderDetailsPage"/> and success page.</summary>
public sealed record OrderDetailsDto
{
    public required string Id { get; init; }
    public required string OrderNumber { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = "SAR";
    public string Status { get; init; } = "";
    public string? StatusLabel { get; init; }
    public DateTime CreatedAt { get; init; }
    public VendorMiniDto? Vendor { get; init; }
    public IReadOnlyList<CartLineDto> Items { get; init; } = Array.Empty<CartLineDto>();
    public string? PickupType { get; init; }
    public string? PickupLabel { get; init; }
    public string? PaymentMethod { get; init; }
    public string? PaymentLabel { get; init; }
    public decimal? CashTendered { get; init; }
    public decimal? ExpectedChange { get; init; }
    public string? CustomerNotes { get; init; }
    public string? CarModel { get; init; }
    public string? CarColor { get; init; }
    public string? CarPlate { get; init; }
    public bool CanCancel { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}
