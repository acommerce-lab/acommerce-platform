using ACommerce.Kits.Taxonomy.Operations;

namespace ACommerce.Kits.Taxonomy.Frontend.Customer.Stores;

/// <summary>store قابِل لِلتَّفاعُل لِشَجَرَات Taxonomy. cache لِكُلّ
/// rootCode حَتّى يُلغى يَدَوياً.</summary>
public interface ITaxonomyStore
{
    /// <summary>قائِمَة مَسطَّحَة بِكُلّ عُقَد شَجَرَة. الواجِهَة تُعيد
    /// التَّركيب الهَرَمي مَن ParentId.</summary>
    Task<IReadOnlyList<ITaxonomyNode>> GetTreeAsync(string rootCode, CancellationToken ct = default);

    /// <summary>عُقدَة بِالـ slug — أَو null إن لَم تَكُن في الـ cache بَعد
    /// (يُستَدعى <see cref="GetTreeAsync"/> أَوَّلاً).</summary>
    ITaxonomyNode? FindByCode(string rootCode, string code);

    /// <summary>مَسار breadcrumb مَن الجَذر إلى الـ slug (in-memory مَن الـ cache).</summary>
    IReadOnlyList<ITaxonomyNode> GetPath(string rootCode, string code);

    /// <summary>أَولاد عُقدَة (أَو الجُذور لَو parentId=null).</summary>
    IReadOnlyList<ITaxonomyNode> GetChildren(string rootCode, Guid? parentId);

    void Invalidate(string rootCode);
}
