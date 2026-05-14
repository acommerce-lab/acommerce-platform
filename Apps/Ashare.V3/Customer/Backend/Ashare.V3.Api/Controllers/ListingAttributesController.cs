using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// قِيَم سِمات إعلان مُحَدَّد (snapshot). الـ V3 endpoint البَسيط
/// لِصَفحَة التَّعديل لِتَجلِب القِيَم الحالِيَّة + تَملأ AttributesEditor.
/// لا يَتَطَلَّب owner check — الإعلان عامّ.
/// </summary>
[ApiController]
public sealed class ListingAttributesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly IAttributeTemplateSource _source;
    public ListingAttributesController(AshareV3DbContext db, IAttributeTemplateSource source)
    {
        _db = db;
        _source = source;
    }

    [HttpGet("/listings/{id:guid}/attributes")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var listing = await _db.ProductListings.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");

        var snapshot = await AttributeSnapshotBuilder.BuildForAsync(_source, listing, ct);
        var withArabic = V3SnapshotPostProcessor.ApplyArabicLabels(snapshot.ToList());
        return this.OkEnvelope("dynamic_attrs.snapshot.get", withArabic);
    }
}
