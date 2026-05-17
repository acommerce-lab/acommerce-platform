using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// قالَب سِمات الفِئَة. ثَلاث مَصادِر بِالتَّرتيب:
/// <list type="number">
///   <item><b>جَداوِل الإنتاج</b> (<c>CategoryAttributeMappings + AttributeDefinitions
///         + AttributeValues</c>) — الكانوني لِلفِئات الإنتاجِيَّة.</item>
///   <item><b>Composite</b> (<see cref="AshareV3CompositeTemplateSource"/>)
///         — لِسلاجات V3-only مَثَل <c>roommate_has</c> / <c>roommate_wants</c>
///         الَّتي لا تَوجَد في asharedb. الـ source يَرُدّ قالَب hardcoded.</item>
///   <item><b>DB-served seed</b> (<c>CategoryAttributeTemplates</c> row) —
///         لِفِئات admin-edited بِلا mappings إنتاجِيَّة.</item>
/// </list>
/// </summary>
[ApiController]
public sealed class CategoryTemplatesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly ProductionAttributeTemplateSource _prodSource;
    private readonly IAttributeTemplateSource _composite;
    public CategoryTemplatesController(
        AshareV3DbContext db,
        ProductionAttributeTemplateSource prodSource,
        IAttributeTemplateSource composite)
    {
        _db = db;
        _prodSource = prodSource;
        _composite = composite;
    }

    [HttpGet("/categories/{slug}/attribute-template")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        slug = (slug ?? "").Trim().ToLowerInvariant();

        // ① مَصدَر الإنتاج (CategoryId مَن slug في asharedb).
        var categoryId = await _db.ProductCategories.AsNoTracking()
            .Where(c => c.Slug == slug).Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (categoryId is Guid id)
        {
            var prod = await _prodSource.BuildForCategoryAsync(id, ct);
            if (prod is { Fields.Count: > 0 })
                return this.OkEnvelope("category.attribute_template", prod);
        }

        // ② Composite: V3-only slugs (roommate_has/roommate_wants).
        // الـ Composite يَكتَشِف الـ slug عَبر Guid مُشتَقّ ثابِت ⇒ يَرُدّ
        // قالَب hardcoded لِلروممَت.
        var roommateScope = slug switch
        {
            "roommate_has"   => Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a2"),
            "roommate_wants" => Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a3"),
            _                => (Guid?)null,
        };
        if (roommateScope is Guid rs)
        {
            var roommate = await _composite.BuildForScopeAsync(rs, ct);
            if (roommate is { Fields.Count: > 0 })
                return this.OkEnvelope("category.attribute_template", roommate);
        }

        // ③ DB-served seed (admin).
        var row = await _db.CategoryAttributeTemplates.AsNoTracking()
            .Where(t => t.CategorySlug == slug).Select(t => t.TemplateJson)
            .FirstOrDefaultAsync(ct);
        AttributeTemplate? seeded = null;
        if (!string.IsNullOrEmpty(row))
            seeded = DynamicAttributeHelper.ParseTemplate(row);

        return this.OkEnvelope("category.attribute_template",
            seeded ?? new AttributeTemplate());
    }
}
