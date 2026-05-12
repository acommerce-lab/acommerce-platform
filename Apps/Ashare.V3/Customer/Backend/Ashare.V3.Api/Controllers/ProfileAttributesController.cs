using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// قِيَم سِمات بروفايل المُستَخدِم الحالي. الـ V3 endpoint البَسيط الَّذي
/// يَدمِج قالَب نِطاق Profile (مَن الكيت) مَع <c>Profile.AttributesJson</c>
/// لِبِناء snapshot واحِد لِلواجِهَة الأَمامِيَّة.
///
/// <para><b>لِماذا لا يَكفي <c>DynamicAttributesController</c> الكَيتي؟</b>
/// لِأَنّ ذاك يَرُدّ القالَب فَقَط (مَيتاداتا). هذا الـ endpoint يَرُدّ
/// <c>DynamicAttribute[]</c> snapshot مَدموج بِالقِيَم — أَنسَب لِصَفحَة Me.</para>
/// </summary>
[ApiController]
public sealed class ProfileAttributesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly IAttributeTemplateSource _source;
    public ProfileAttributesController(AshareV3DbContext db, IAttributeTemplateSource source)
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
        if (CallerUserId is null) return this.UnauthorizedEnvelope();

        var profile = await _db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == CallerUserId, ct);
        if (profile is null) return this.NotFoundEnvelope("profile_not_found");

        var snapshot = await AttributeSnapshotBuilder.BuildForAsync(_source, profile, ct);
        return this.OkEnvelope("dynamic_attrs.snapshot.get", snapshot);
    }
}
