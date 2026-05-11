using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// يَعرِض قَوالِب السِمات الديناميكِيَّة لِكُلّ فِئَة. الواجِهَة تَطلُب
/// قالَب الفِئَة المُختارَة في صَفحَة إنشاء/تَعديل العَرض، وتُمَرِّره إلى
/// <c>AcAttrEditor</c> لِيَرسُم الحُقول تِلقائيّاً.
///
/// <para>المَصدَر: جَدول <c>CategoryAttributeTemplates</c> (يُملأ مِن
/// <see cref="V3CategoryTemplates"/> في Bootstrap). لَو الـ row مَفقود
/// لِأَيّ سَبَب، fallback مُباشِر إلى الكود — يَضمَن أَنّ الواجِهَة لا
/// تَفشَل حَتّى لَو Bootstrap لَم يَجرِ بَعد.</para>
/// </summary>
[ApiController]
public sealed class CategoryTemplatesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    public CategoryTemplatesController(AshareV3DbContext db) => _db = db;

    [HttpGet("/categories/{slug}/attribute-template")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        slug = (slug ?? "").Trim().ToLowerInvariant();

        // ① DB أَوَّلاً (المَعروض، قابِل لِلتَعديل عَبر لوحَة التَحَكُّم).
        var row = await _db.CategoryAttributeTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CategorySlug == slug, ct);

        AttributeTemplate? template = row is not null
            ? DynamicAttributeHelper.ParseTemplate(row.TemplateJson)
            : null;

        // ② Fallback إلى الكود (لَو DB row مَفقود أَو JSON فاسِد).
        if (template is null)
        {
            var hit = V3CategoryTemplates.All.FirstOrDefault(t => t.Slug == slug);
            template = hit.Template;
        }

        // ③ لا قالَب مُعَرَّف لِهذه الفِئَة ⇒ envelope فارِغ (الواجِهَة تَعرِض
        // فَقَط حُقول العَرض الأَساسِيَّة).
        return this.OkEnvelope("category.attribute_template",
            template ?? new AttributeTemplate());
    }
}
