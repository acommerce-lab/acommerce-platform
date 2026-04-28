using Microsoft.AspNetCore.Components;

namespace ACommerce.Templates.Merchant.Commerce.Models;

// ── DTOs للوحة تحكم التاجر ───────────────────────────────────────────────
// يتبع نفس مبدأ SharedModels: حقول محددة + Extra bag للتوسع.

/// <summary>
/// بطاقة إحصاء في لوحة تحكم التاجر (مبيعات اليوم، الطلبات المعلّقة...).
/// </summary>
public sealed record MerchantStatDto
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public string? IconName { get; init; }
    public string? Trend { get; init; }   // "up" | "down" | null
    public string? TrendValue { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// صف طلب في قائمة طلبات التاجر.
/// </summary>
public sealed record MerchantOrderRowDto
{
    public required string Id { get; init; }
    public required string CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public required decimal Total { get; init; }
    public string Currency { get; init; } = "SAR";
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<MerchantOrderItemDto> Items { get; init; } = Array.Empty<MerchantOrderItemDto>();
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>بند منتج داخل الطلب.</summary>
public sealed record MerchantOrderItemDto
{
    public required string Title { get; init; }
    public string? Emoji { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

/// <summary>
/// صف منتج/عرض في قائمة عروض التاجر.
/// </summary>
public sealed record MerchantOfferRowDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Emoji { get; init; }
    public required decimal Price { get; init; }
    public string Currency { get; init; } = "SAR";
    public bool IsAvailable { get; init; }
    public string? Category { get; init; }
    public int? Stock { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// شريحة وقت في جدول التاجر.
/// </summary>
public sealed record MerchantScheduleSlotDto
{
    public required string DayLabel { get; init; }
    public required string OpenTime { get; init; }
    public required string CloseTime { get; init; }
    public bool IsOpen { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// نموذج إنشاء/تعديل عرض — يُستخدم في AcVendorOfferForm.
/// </summary>
public sealed record OfferFormModel
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "SAR";
    public string? Emoji { get; set; }
    public string? Category { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int? Stock { get; set; }
}
