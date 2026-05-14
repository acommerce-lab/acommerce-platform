using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// قالَب سِمات الفِئَة. مَصدَرَين فَقَط، لا كود ثابِت:
/// <list type="number">
///   <item><b>جَداوِل الإنتاج</b> (<c>CategoryAttributeMappings +
///         AttributeDefinitions + AttributeValues</c>) — الكانوني،
///         يَحوي التَسميات الفِعلِيَّة.</item>
///   <item><b>DB-served seed</b> (<c>CategoryAttributeTemplates</c>
///         row) — لِفِئات لَيس لَها mappings إنتاجِيَّة بَعد، يَمتَلِئ
///         يَدَوِيّاً مِن لوحَة التَحَكُّم.</item>
/// </list>
/// لَو كِلاهُما فارِغ ⇒ template فارِغ ⇒ الواجِهَة لا تَعرِض سِمات.
/// </summary>
[ApiController]
public sealed class CategoryTemplatesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly ProductionAttributeTemplateSource _prodSource;
    public CategoryTemplatesController(AshareV3DbContext db, ProductionAttributeTemplateSource prodSource)
    {
        _db = db;
        _prodSource = prodSource;
    }

    [HttpGet("/categories/{slug}/attribute-template")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        slug = (slug ?? "").Trim().ToLowerInvariant();

        // ① مَصدَر الإنتاج.
        var categoryId = await _db.ProductCategories.AsNoTracking()
            .Where(c => c.Slug == slug).Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (categoryId is Guid id)
        {
            var prod = await _prodSource.BuildForCategoryAsync(id, ct);
            if (prod is { Fields.Count: > 0 })
                return this.OkEnvelope("category.attribute_template", prod);
        }

        // ② DB-served seed (admin).
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
