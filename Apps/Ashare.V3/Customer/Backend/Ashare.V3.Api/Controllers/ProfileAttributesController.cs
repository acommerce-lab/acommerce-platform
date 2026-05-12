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
using System.Text.Json;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// سِمات البروفايل الديناميكِيَّة. تُعامِل أَعمِدَة <see cref="ProfileEntity"/>
/// الَّتي لَيسَت في <c>IUserProfile</c> الواجِهَة (NationalId/BusinessName/
/// Address/Country/PostalCode/Coordinates) كَخَصائِص قابِلَة لِلتَخصيص
/// مَن نَفس مُحَرِّك القَوالِب الَّذي يُغَذّي الإعلانات.
///
/// <para>المَصدَر الكانوني: جَداوِل <c>AttributeDefinitions</c> +
/// <c>CategoryAttributeMappings</c> مَع sentinel
/// <see cref="V3ProfileAttributes.CategoryId"/>. لَو ناقِصَة، الـ Bootstrap
/// يُضيف seed عِندَ الإقلاع. القِيَم تُقرَأ/تُكتَب عَلى أَعمِدَة Profile
/// مُباشِرَةً بِـ reflection عَلى <c>AttributeDefinition.Code</c> ⇒ لا
/// AttributesJson عَلى Profile.</para>
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

    /// <summary>قالَب سِمات البروفايل — مَفتوح (لا يَتَطَلَّب مُصادَقَة).</summary>
    [HttpGet("/profile/attribute-template")]
    public async Task<IActionResult> GetTemplate(CancellationToken ct)
    {
        var tpl = await _prodSource.BuildForCategoryAsync(V3ProfileAttributes.CategoryId, ct)
                   ?? new AttributeTemplate();
        return this.OkEnvelope("profile.attribute_template", tpl);
    }

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

    public sealed record SaveBody(Dictionary<string, JsonElement>? Values);

    /// <summary>حِفظ قِيَم سِمات البروفايل — يَكتُب عَلى أَعمِدَة Profile بِـ reflection.</summary>
    [Authorize]
    [HttpPost("/profile/me/attributes")]
    public async Task<IActionResult> SaveMine([FromBody] SaveBody body, CancellationToken ct)
    {
        if (CallerUserId is null) return this.UnauthorizedEnvelope();
        if (body.Values is null || body.Values.Count == 0)
            return this.OkEnvelope("profile.attributes.save", new { ok = true });

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == CallerUserId, ct);
        if (profile is null) return this.NotFoundEnvelope("profile_not_found");

        var tpl = await _prodSource.BuildForCategoryAsync(V3ProfileAttributes.CategoryId, ct);
        if (tpl is null || tpl.Fields.Count == 0)
            return this.OkEnvelope("profile.attributes.save", new { ok = true });

        // مَفاتيح القالَب فَقَط مَسموحَة — يَحمي مَن كِتابَة UserId/Id/IsDeleted.
        var allowed = tpl.Fields.ToDictionary(f => f.Key, f => f, StringComparer.Ordinal);
        foreach (var (key, el) in body.Values)
        {
            if (!allowed.TryGetValue(key, out var field)) continue;
            var prop = typeof(ProfileEntity).GetProperty(
                field.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null || !prop.CanWrite) continue;
            var converted = ConvertElement(el, prop.PropertyType);
            prop.SetValue(profile, converted);
        }
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return this.OkEnvelope("profile.attributes.save", new { ok = true });
    }

    /// <summary>
    /// تَحويل قِيمَة JSON إلى نَوع الخاصِّيَّة. أَنواع البروفايل في V3 كُلّها
    /// <c>string?</c> حاليّاً — لكِن نَدعَم int/decimal/bool لِمُرونَة مُستَقبَلِيَّة.
    /// </summary>
    private static object? ConvertElement(JsonElement el, Type targetType)
    {
        var nonNull = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (el.ValueKind == JsonValueKind.Null) return null;
        if (nonNull == typeof(string))   return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
        if (nonNull == typeof(int))      return el.ValueKind == JsonValueKind.Number ? el.GetInt32() : int.TryParse(el.ToString(), out var i) ? i : (object?)null;
        if (nonNull == typeof(long))     return el.ValueKind == JsonValueKind.Number ? el.GetInt64() : long.TryParse(el.ToString(), out var l) ? l : (object?)null;
        if (nonNull == typeof(decimal))  return el.ValueKind == JsonValueKind.Number ? el.GetDecimal() : decimal.TryParse(el.ToString(), out var d) ? d : (object?)null;
        if (nonNull == typeof(bool))     return el.ValueKind == JsonValueKind.True || (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out var b) && b);
        if (nonNull == typeof(DateTime)) return el.TryGetDateTime(out var dt) ? dt : (object?)null;
        return el.ToString();
    }
}
