using ACommerce.Kits.Taxonomy.Backend;
using ACommerce.Kits.Taxonomy.Operations;

namespace Ashare.V3.Data.Templates;

/// <summary>
/// تَنفيذ في الذاكِرَة لِـ <see cref="ITaxonomyStore"/> في Ashare V3 —
/// شَجَرَة قَصيرَة (kind واحِد بِـ leaves اثنَين) لا تَستَحِقّ جَدول DB.
///
/// <para>الـ stakeholder طَلَب نَوعَين فَقَط لِلأَفراد:</para>
/// <list type="bullet">
///   <item><c>roommate_has</c> — "عَشير عَنده سَكَن" (يَعرِض غُرفَة أَو
///         سَكَن)</item>
///   <item><c>roommate_wants</c> — "عَشير يَدور سَكَن" (يَبحَث عَن سَكَن
///         مُشتَرَك)</item>
/// </list>
///
/// <para>الـ slug في <see cref="IListing.PropertyType"/> يَحفَظ
/// <c>roommate_has</c> أَو <c>roommate_wants</c>. لا تَغيير عَلى schema
/// asharedb. لا migration. الإدارَة الكامِلَة لِلتَّصنيف هُنا في كود
/// التَطبيق — يُناسِب صَغر النِطاق (٣ عُقَد فَقَط).</para>
/// </summary>
public sealed class AshareV3TaxonomyStore : ITaxonomyStore
{
    private const string Root = "listing_categories";

    // الـ kind الأَب
    private static readonly TaxonomyNodeView Roommate = new(
        Id:        Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a1"),
        ParentId:  null,
        RootCode:  Root,
        Code:      "roommate",
        Name:      "Roommate",
        NameAr:    "سَكَن مُشتَرَك",
        Icon:      "users",
        SortOrder: 1,
        IsActive:  true);

    private static readonly TaxonomyNodeView RoommateHas = new(
        Id:        Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a2"),
        ParentId:  Roommate.Id,
        RootCode:  Root,
        Code:      "roommate_has",
        Name:      "Has a room",
        NameAr:    "عَنده سَكَن",
        Icon:      "🏠",
        SortOrder: 1,
        IsActive:  true);

    private static readonly TaxonomyNodeView RoommateWants = new(
        Id:        Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a3"),
        ParentId:  Roommate.Id,
        RootCode:  Root,
        Code:      "roommate_wants",
        Name:      "Looking for a room",
        NameAr:    "يَدور سَكَن",
        Icon:      "🔍",
        SortOrder: 2,
        IsActive:  true);

    private static readonly IReadOnlyList<ITaxonomyNode> Tree = new ITaxonomyNode[]
    {
        Roommate, RoommateHas, RoommateWants,
    };

    public Task<IReadOnlyList<ITaxonomyNode>> GetTreeAsync(string rootCode, CancellationToken ct)
    {
        if (!string.Equals(rootCode, Root, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<IReadOnlyList<ITaxonomyNode>>(Array.Empty<ITaxonomyNode>());
        return Task.FromResult(Tree);
    }

    public Task<IReadOnlyList<string>> GetSubtreeSlugsAsync(string rootCode, string code, CancellationToken ct)
    {
        var node = Tree.FirstOrDefault(n => string.Equals(n.Code, code, StringComparison.OrdinalIgnoreCase));
        if (node is null) return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        var result = new List<string> { node.Code };
        result.AddRange(Tree.Where(n => n.ParentId == node.Id).Select(n => n.Code));
        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    public Task<IReadOnlyList<ITaxonomyNode>> GetPathAsync(string rootCode, string code, CancellationToken ct)
    {
        var leaf = Tree.FirstOrDefault(n => string.Equals(n.Code, code, StringComparison.OrdinalIgnoreCase));
        if (leaf is null) return Task.FromResult<IReadOnlyList<ITaxonomyNode>>(Array.Empty<ITaxonomyNode>());
        var path = new List<ITaxonomyNode> { leaf };
        var cur = leaf;
        while (cur.ParentId is { } pid)
        {
            var parent = Tree.FirstOrDefault(n => n.Id == pid);
            if (parent is null) break;
            path.Insert(0, parent);
            cur = parent;
        }
        return Task.FromResult<IReadOnlyList<ITaxonomyNode>>(path);
    }

    public Task<ITaxonomyNode?> GetByCodeAsync(string rootCode, string code, CancellationToken ct)
        => Task.FromResult<ITaxonomyNode?>(
            Tree.FirstOrDefault(n => string.Equals(n.Code, code, StringComparison.OrdinalIgnoreCase)));

    // إدارَة CRUD لا تَنطَبِق — الشَجَرَة hardcoded.
    public Task<bool> CreateNoSaveAsync(TaxonomyNodeUpsert input, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> UpdateNoSaveAsync(Guid id, TaxonomyNodeUpsert patch, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> DeleteNoSaveAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
}
