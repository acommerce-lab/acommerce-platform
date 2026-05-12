using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// سِمات البروفايل الديناميكِيَّة — <b>عَرض فَقَط</b>. الجَدول مُحَدَّد
/// بِواجِهَة <c>IUserProfile</c>؛ كُلّ الزِيادات تُخَزَّن في
/// <c>Profile.AttributesJson</c>. التَسميات والتَرتيب تَأتي مِن جَدول
/// <c>AttributeDefinitions</c> + <c>CategoryAttributeMappings</c> مَع
/// sentinel <see cref="V3ProfileAttributes.CategoryId"/>.
///
/// <para><b>لا POST</b>: الجَدول المَركَزي مَيتاداتا فَقَط — تَعديل
/// القِيَم يَجري خارِج الواجِهَة الزَبونِيَّة.</para>
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

    /// <summary>قِيَم سِمات البروفايل الحالي (snapshot يَدمِج القالَب مَع AttributesJson).</summary>
    [Authorize]
    [HttpGet("/profile/me/attributes")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (CallerUserId is null) return this.UnauthorizedEnvelope();
        var attrsJson = await _db.Profiles.AsNoTracking()
            .Where(p => p.UserId == CallerUserId)
            .Select(p => p.AttributesJson)
            .FirstOrDefaultAsync(ct);
        if (attrsJson is null && !await _db.Profiles.AsNoTracking().AnyAsync(p => p.UserId == CallerUserId, ct))
            return this.NotFoundEnvelope("profile_not_found");

        var tpl = await _prodSource.BuildForCategoryAsync(V3ProfileAttributes.CategoryId, ct)
                   ?? new AttributeTemplate();

        var values = ParseFlat(attrsJson);
        var snapshot = DynamicAttributeHelper.BuildSnapshot(tpl, values);
        return this.OkEnvelope("profile.attributes.get", snapshot);
    }

    private static Dictionary<string, object?> ParseFlat(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return new();
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                    System.Text.Json.JsonValueKind.Number => prop.Value.TryGetInt64(out var i) ? i : (object)prop.Value.GetDouble(),
                    System.Text.Json.JsonValueKind.True   => true,
                    System.Text.Json.JsonValueKind.False  => false,
                    System.Text.Json.JsonValueKind.Null   => null,
                    _ => prop.Value.GetRawText(),
                };
            }
            return result;
        }
        catch { return new(); }
    }
}
