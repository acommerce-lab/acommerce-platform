namespace ACommerce.Templates.Admin.Models;

// ── DTOs للوحة تحكم الإدارة ──────────────────────────────────────────────

/// <summary>
/// صف مستخدم في لوحة الإدارة.
/// </summary>
public sealed record AdminUserRowDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public required string Status { get; init; }  // "active" | "suspended" | "pending"
    public string? Role { get; init; }
    public DateTime JoinedAt { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// صف بائع/متجر في لوحة الإدارة.
/// </summary>
public sealed record AdminVendorRowDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Phone { get; init; }
    public required string Status { get; init; }  // "active" | "suspended" | "pending_review"
    public string? Category { get; init; }
    public int OrderCount { get; init; }
    public decimal TotalRevenue { get; init; }
    public string Currency { get; init; } = "SAR";
    public DateTime CreatedAt { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// مقياس إحصائي على مستوى المنصة.
/// </summary>
public sealed record PlatformMetricDto
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public string? IconName { get; init; }
    public string? Trend { get; init; }      // "up" | "down" | null
    public string? TrendValue { get; init; }
    public string? Period { get; init; }     // "اليوم" | "الأسبوع" | "الشهر"
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// فلتر البحث المشترك في صفحات الإدارة.
/// </summary>
public sealed record AdminFilterDto
{
    public string? Query { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
