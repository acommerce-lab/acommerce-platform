using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACommerce.Kits.Taxonomy.Backend;

/// <summary>
/// مَتَحَكِّم Taxonomy المَفتوح — صَفَحات العَملاء تَستَدعيه لِبِناء
/// قَوائِم اختِيار شَجَرِيَّة (categories, locations, …). الإدارَة CRUD
/// تَحتاج Authorize (سَيُضاف policy لاحِقاً عَبر admin bundle).
/// </summary>
[ApiController]
[Route("taxonomy")]
public sealed class TaxonomyController : ControllerBase
{
    private readonly ITaxonomyStore _store;
    public TaxonomyController(ITaxonomyStore store) => _store = store;

    /// <summary>كُلّ عُقَد شَجَرَة (flat) — الواجِهَة الأَمامِيَّة تُعيد
    /// تَركيبها هَرَمياً مَن ParentId.</summary>
    [HttpGet("{rootCode}")]
    public async Task<IActionResult> GetTree(string rootCode, CancellationToken ct)
    {
        var nodes = await _store.GetTreeAsync(rootCode, ct);
        return this.OkEnvelope("taxonomy.tree.get", nodes);
    }

    /// <summary>المَسار مَن الجَذر إلى العُقدَة (breadcrumb).</summary>
    [HttpGet("{rootCode}/path/{code}")]
    public async Task<IActionResult> GetPath(string rootCode, string code, CancellationToken ct)
    {
        var path = await _store.GetPathAsync(rootCode, code, ct);
        if (path.Count == 0) return this.NotFoundEnvelope("node_not_found");
        return this.OkEnvelope("taxonomy.path.get", path);
    }

    /// <summary>كُلّ الـ slugs تَحت عُقدَة (يَشمُل العُقدَة نَفسها).</summary>
    [HttpGet("{rootCode}/subtree/{code}")]
    public async Task<IActionResult> GetSubtree(string rootCode, string code, CancellationToken ct)
    {
        var slugs = await _store.GetSubtreeSlugsAsync(rootCode, code, ct);
        return this.OkEnvelope("taxonomy.subtree.get", slugs);
    }
}
