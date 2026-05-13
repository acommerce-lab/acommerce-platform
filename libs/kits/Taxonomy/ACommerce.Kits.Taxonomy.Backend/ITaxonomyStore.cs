using ACommerce.Kits.Taxonomy.Operations;

namespace ACommerce.Kits.Taxonomy.Backend;

/// <summary>
/// المَنفَذ الَّذي يُنَفِّذه التَطبيق لِيَربِط شَجَرَة Taxonomy بِتَخزينه
/// (عادَةً جَدول EF واحِد). الكيت لا يَفرِض شَكلاً — adjacency-list أَو
/// materialized-path كِلاهُما مَقبول طالَما يُغَطّي الـ contract.
/// </summary>
public interface ITaxonomyStore
{
    /// <summary>كُلّ عُقَد شَجَرَة واحِدَة (flat) — الواجِهَة الأَمامِيَّة
    /// تُعيد بِناءها هَرَمياً مَن <c>ParentId</c>.</summary>
    Task<IReadOnlyList<ITaxonomyNode>> GetTreeAsync(string rootCode, CancellationToken ct);

    /// <summary>الـ slugs الكامِلَة تَحت عُقدَة (تَشمُل العُقدَة نَفسها).
    /// تُستَخدَم لِبِناء <c>WHERE PropertyType IN (...)</c> لِفِلتَرَة
    /// الإعلانات تَحت فِئَة عُليا.</summary>
    Task<IReadOnlyList<string>> GetSubtreeSlugsAsync(string rootCode, string code, CancellationToken ct);

    /// <summary>المَسار مَن الجَذر إلى العُقدَة (لِـ breadcrumb).
    /// تُرجِع العُقَد بِالتَّرتيب: [root, …, leaf].</summary>
    Task<IReadOnlyList<ITaxonomyNode>> GetPathAsync(string rootCode, string code, CancellationToken ct);

    /// <summary>عُقدَة واحِدَة بِالـ slug، أَو null.</summary>
    Task<ITaxonomyNode?> GetByCodeAsync(string rootCode, string code, CancellationToken ct);

    // ─── إدارَة (admin) — tracker-only، الـ controller يَحفَظ في SaveAtEnd ──
    Task<bool> CreateNoSaveAsync(TaxonomyNodeUpsert input, CancellationToken ct);
    Task<bool> UpdateNoSaveAsync(Guid id, TaxonomyNodeUpsert patch, CancellationToken ct);
    Task<bool> DeleteNoSaveAsync(Guid id, CancellationToken ct);
}

/// <summary>حُمولَة إنشاء/تَحديث عُقدَة (PATCH semantics لِلتَحديث).</summary>
public sealed record TaxonomyNodeUpsert(
    string?  RootCode  = null,
    string?  Code      = null,
    string?  Name      = null,
    string?  NameAr    = null,
    string?  Icon      = null,
    int?     SortOrder = null,
    Guid?    ParentId  = null,
    bool?    IsActive  = null);
