using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.OperationEngine.Wire.Http;
using Ejar.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ejar.Api.Controllers;

/// <summary>
/// snapshot سِمات بروفايل المُستَخدِم الحالي (مَدموجَة مَع قالَب Ejar
/// الديناميكي). /me و /profile/edit يَستَهلِكانه.
/// </summary>
[ApiController]
public sealed class EjarProfileAttributesController : ControllerBase
{
    private readonly EjarDbContext _db;
    private readonly IAttributeTemplateSource _source;

    public EjarProfileAttributesController(EjarDbContext db, IAttributeTemplateSource source)
    {
        _db = db;
        _source = source;
    }

    private string? CallerUserId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [Authorize]
    [HttpGet("/profile/me/attributes")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (CallerUserId is null || !Guid.TryParse(CallerUserId, out var uid))
            return this.UnauthorizedEnvelope();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct);
        if (user is null) return this.NotFoundEnvelope("profile_not_found");

        var snapshot = await AttributeSnapshotBuilder.BuildForAsync(_source, user, ct);
        return this.OkEnvelope("dynamic_attrs.snapshot.get", snapshot);
    }
}

/// <summary>snapshot سِمات إعلان مُحَدَّد لِصَفحَة EditListing.</summary>
[ApiController]
public sealed class EjarListingAttributesController : ControllerBase
{
    private readonly EjarDbContext _db;
    private readonly IAttributeTemplateSource _source;

    public EjarListingAttributesController(EjarDbContext db, IAttributeTemplateSource source)
    {
        _db = db;
        _source = source;
    }

    [HttpGet("/listings/{id:guid}/attributes")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var listing = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");

        var snapshot = await AttributeSnapshotBuilder.BuildForAsync(_source, listing, ct);
        return this.OkEnvelope("dynamic_attrs.snapshot.get", snapshot);
    }
}
