using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Ashare.V3.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Security.Claims;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// سِمات البروفايل الديناميكِيَّة — <b>عَرض فَقَط</b>. تُعامِل أَعمِدَة
/// <see cref="ProfileEntity"/> الَّتي لَيسَت في <c>IUserProfile</c> الواجِهَة
/// (NationalId/BusinessName/Address/Country/PostalCode/Coordinates) كَخَصائِص
/// قابِلَة لِلعَرض مَن نَفس مُحَرِّك القَوالِب الَّذي يُغَذّي الإعلانات.
///
/// <para>المَصدَر الكانوني لِلتَسمِيات والتَرتيب: جَداوِل
/// <c>AttributeDefinitions</c> + <c>CategoryAttributeMappings</c> مَع sentinel
/// <see cref="V3ProfileAttributes.CategoryId"/>. الـ Bootstrap يَزرَع
/// definitions/mappings عِندَ الإقلاع إن غابَت. القِيَم تُقرَأ مِن أَعمِدَة
/// Profile مُباشَرَةً بِـ reflection عَلى <c>AttributeDefinition.Code</c>
/// (يُطابِق اسم property بِالضَّبط).</para>
///
/// <para><b>لا POST</b>: الجَدول المَركَزي هو مَصدَر مَيتاداتا فَقَط؛
/// تَعديل القِيَم يَجري خارِج الواجِهَة الزَبونِيَّة (Nafath/admin).</para>
/// </summary>
[ApiController]
public sealed class ProfileAttributesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly ProductionAttributeTemplateSource _prodSource;
    public ProfileAttributesController(
        AshareV3DbContext db, ProductionAttributeTemplateSource prodSource)
    {
        _db = db;
        _prodSource = prodSource;
    }

    private string? CallerUserId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>قِيَم سِمات البروفايل الحالي (snapshot يَدمِج القالَب مَع الأَعمِدَة).</summary>
    [Authorize]
    [HttpGet("/profile/me/attributes")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (CallerUserId is null) return this.UnauthorizedEnvelope();
        var profile = await _db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == CallerUserId, ct);
        if (profile is null) return this.NotFoundEnvelope("profile_not_found");

        var tpl = await _prodSource.BuildForCategoryAsync(V3ProfileAttributes.CategoryId, ct)
                   ?? new AttributeTemplate();

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in tpl.Fields)
        {
            var prop = typeof(ProfileEntity).GetProperty(
                f.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null) continue;
            values[f.Key] = prop.GetValue(profile);
        }

        var snapshot = DynamicAttributeHelper.BuildSnapshot(tpl, values);
        return this.OkEnvelope("profile.attributes.get", snapshot);
    }
}
