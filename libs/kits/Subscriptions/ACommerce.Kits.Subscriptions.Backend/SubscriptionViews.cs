namespace ACommerce.Kits.Subscriptions.Backend;

/// <summary>
/// عقد الاشتراك المرئيّ للواجهة. الـ store يُسلّمه — الكيان الفعليّ في DB
/// قد يحوي حقولاً إضافيّة (ميتاداتا، عداد استخدام، سجلّ تجديد، …) لكنّ
/// هذه الحقول الـ ١٢ هي ما يهمّ كل واجهة تجارة.
/// </summary>
public sealed record SubscriptionView(
    string   Id,
    string?  PlanId,
    string   PlanName,
    string   Status,            // "active" | "expired" | "cancelled"
    DateTime StartDate,
    DateTime EndDate,
    int      ListingsLimit,     // 0 = unlimited
    int      FeaturedLimit,
    int      ImagesPerListing,
    decimal  Price = 0m,
    int      DaysRemaining = 0,
    int      ListingsUsed = 0,
    int      FeaturedUsed = 0);

/// <summary>عقد الباقة في قائمة الـ /plans.</summary>
public sealed record PlanView(
    string Id,
    string Name,
    string Description,
    decimal Price,
    string  Unit,                // "monthly" | "yearly" | …
    int     ListingQuota,
    int     FeaturedQuota,
    int     ImagesPerListing,
    bool    Popular,
    IReadOnlyList<string> Features);

/// <summary>عقد الفاتورة في /me/invoices.</summary>
public sealed record InvoiceView(
    string   Id,
    DateTime CreatedAt,
    decimal  Amount,
    string   Status,             // "paid" | "pending" | "failed"
    string   Method,             // "manual" | "card" | "wallet"
    string?  PlanName,
    string?  Reference);
