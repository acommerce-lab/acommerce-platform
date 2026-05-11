using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// يَعرِض قَوالِب السِمات الديناميكِيَّة لِكُلّ فِئَة. ثَلاث طَبَقات
/// تَدَرُّجِيَّة:
/// <list type="number">
///   <item><b>جَداوِل الإنتاج</b> (<c>CategoryAttributeMappings +
///         AttributeDefinitions + AttributeValues</c>) عَبر
///         <see cref="ProductionAttributeTemplateSource"/>. هذا هو
///         المَصدَر الكانوني لِبَيانات أَشاره الإنتاجِيَّة.</item>
///   <item><b>DB-served seed</b> (<c>CategoryAttributeTemplates</c>
///         row بِـ Slug). يُملأ مِن الكود في Bootstrap، يُحَرَّر
///         عَبر لوحَة التَحَكُّم. مَفيد لِفِئات لَيس لَها mappings
///         إنتاجِيَّة بَعد.</item>
///   <item><b>Code fallback</b> (<see cref="V3CategoryTemplates"/>) —
///         الضَمانَة الأَخيرَة لِيَعمَل dev بِلا clone.</item>
/// </list>
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

        // ① مَصدَر الإنتاج. يَحتاج Guid → نَحُلّ Slug → Id أَوَّلاً.
        var categoryId = await _db.ProductCategories.AsNoTracking()
            .Where(c => c.Slug == slug).Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (categoryId is Guid id)
        {
            var prod = await _prodSource.BuildForCategoryAsync(id, ct);
            if (prod is { Fields.Count: > 0 })
                return this.OkEnvelope("category.attribute_template", prod);
        }

        // ② DB-served seed (CategoryAttributeTemplates).
        var row = await _db.CategoryAttributeTemplates.AsNoTracking()
            .Where(t => t.CategorySlug == slug).Select(t => t.TemplateJson)
            .FirstOrDefaultAsync(ct);
        AttributeTemplate? seeded = null;
        if (!string.IsNullOrEmpty(row))
            seeded = DynamicAttributeHelper.ParseTemplate(row);
        if (seeded is { Fields.Count: > 0 })
            return this.OkEnvelope("category.attribute_template", seeded);

        // ③ Code fallback.
        var hit = V3CategoryTemplates.All.FirstOrDefault(t => t.Slug == slug);
        return this.OkEnvelope("category.attribute_template",
            hit.Template ?? new AttributeTemplate());
    }
}
