using ACommerce.Kits.Taxonomy.Backend;
using ACommerce.Kits.Taxonomy.Operations;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Data.Stores;

/// <summary>
/// تَنفيذ <see cref="ITaxonomyStore"/> فَوق جَدول <see cref="TaxonomyNodeEntity"/>
/// — مَطابِق لِـ <c>EjarTaxonomyStore</c>. يَستَبدِل <c>AshareV3TaxonomyStore</c>
/// الـ in-memory.
///
/// <para>الـ Tree-shape rebuild يَجري في الواجِهَة الأَمامِيَّة — هنا نَرُدّ
/// flat list مَع كُلّ ما تَحتاج (ParentId, RootCode, SortOrder) لِبِناء
/// الشَجَرَة. الـ <c>AcTaxonomyChips</c>/<c>AcTaxonomyTreeSelect</c> widgets
/// الَّتي تَستَخدِمها صَفَحات Marketplace تَتَوَلّى ذلك.</para>
/// </summary>
public sealed class AshareV3DbTaxonomyStore : ITaxonomyStore
{
    private readonly AshareV3DbContext _db;
    public AshareV3DbTaxonomyStore(AshareV3DbContext db) => _db = db;

    public async Task<IReadOnlyList<ITaxonomyNode>> GetTreeAsync(string rootCode, CancellationToken ct)
    {
        var rows = await _db.TaxonomyNodes.AsNoTracking()
            .Where(t => t.RootCode == rootCode && t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Code)
            .ToListAsync(ct);
        return rows.Cast<ITaxonomyNode>().ToList();
    }

    public async Task<IReadOnlyList<string>> GetSubtreeSlugsAsync(string rootCode, string code, CancellationToken ct)
    {
        var all = await _db.TaxonomyNodes.AsNoTracking()
            .Where(t => t.RootCode == rootCode && t.IsActive)
            .Select(t => new { t.Id, t.ParentId, t.Code })
            .ToListAsync(ct);

        var root = all.FirstOrDefault(n => string.Equals(n.Code, code, StringComparison.OrdinalIgnoreCase));
        if (root is null) return Array.Empty<string>();

        var byParent = all.GroupBy(n => n.ParentId)
                          .ToDictionary(g => g.Key ?? Guid.Empty, g => g.ToList());
        var result = new List<string>();
        var queue = new Queue<Guid>();
        queue.Enqueue(root.Id);
        result.Add(root.Code);
        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            if (!byParent.TryGetValue(pid, out var kids)) continue;
            foreach (var ch in kids)
            {
                result.Add(ch.Code);
                queue.Enqueue(ch.Id);
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<ITaxonomyNode>> GetPathAsync(string rootCode, string code, CancellationToken ct)
    {
        var all = await _db.TaxonomyNodes.AsNoTracking()
            .Where(t => t.RootCode == rootCode && t.IsActive)
            .ToListAsync(ct);
        var byId = all.ToDictionary(n => n.Id);

        var leaf = all.FirstOrDefault(n => string.Equals(n.Code, code, StringComparison.OrdinalIgnoreCase));
        if (leaf is null) return Array.Empty<ITaxonomyNode>();

        var path = new List<ITaxonomyNode> { leaf };
        var cur = leaf;
        while (cur.ParentId is { } pid && byId.TryGetValue(pid, out var parent))
        {
            path.Insert(0, parent);
            cur = parent;
        }
        return path.Cast<ITaxonomyNode>().ToList();
    }

    public async Task<ITaxonomyNode?> GetByCodeAsync(string rootCode, string code, CancellationToken ct)
    {
        return await _db.TaxonomyNodes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.RootCode == rootCode && t.Code == code, ct);
    }

    public async Task<bool> CreateNoSaveAsync(TaxonomyNodeUpsert input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.RootCode) || string.IsNullOrEmpty(input.Code) || string.IsNullOrEmpty(input.Name))
            return false;
        _db.TaxonomyNodes.Add(new TaxonomyNodeEntity
        {
            Id        = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ParentId  = input.ParentId,
            RootCode  = input.RootCode!,
            Code      = input.Code!,
            Name      = input.Name!,
            NameAr    = input.NameAr,
            Icon      = input.Icon,
            SortOrder = input.SortOrder ?? 0,
            IsActive  = input.IsActive ?? true,
        });
        return await Task.FromResult(true);
    }

    public async Task<bool> UpdateNoSaveAsync(Guid id, TaxonomyNodeUpsert patch, CancellationToken ct)
    {
        var n = await _db.TaxonomyNodes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (n is null) return false;
        if (patch.Name      is not null) n.Name      = patch.Name;
        if (patch.NameAr    is not null) n.NameAr    = patch.NameAr;
        if (patch.Icon      is not null) n.Icon      = patch.Icon;
        if (patch.SortOrder is not null) n.SortOrder = patch.SortOrder.Value;
        if (patch.IsActive  is not null) n.IsActive  = patch.IsActive.Value;
        if (patch.ParentId  is not null) n.ParentId  = patch.ParentId;
        n.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<bool> DeleteNoSaveAsync(Guid id, CancellationToken ct)
    {
        var n = await _db.TaxonomyNodes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (n is null) return false;
        n.IsDeleted = true;
        n.UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
