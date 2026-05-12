using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// سِمات البروفايل الديناميكِيَّة لِـ V3. مَسؤولِيَّة التَطبيق (لا الـ kit)
/// — تَماماً كَما طَلَب المُستَخدِم: "النطاق + الترحيلات" خاصَّة بِالتَطبيق.
///
/// <para>الـ endpoints الثَلاث:</para>
/// <list type="bullet">
///   <item><c>GET /profile/attribute-template</c> — يُرجِع <c>AttributeTemplate</c>
///         (DB row بِـ slug="profile"). لا fallback إلى كود — لَو لا row،
///         template فارِغ ⇒ صَفحَة البروفايل لا تَعرِض حُقولاً ديناميكِيَّة.
///         تَستَخدِمه واجِهَة تَحرير البروفايل لِتَرسُم <c>AcAttrEditor</c>.</item>
///   <item><c>GET /profile/me/attributes</c> — snapshots مُكتَمِلَة (تَطبيق
///         القالَب الحالي عَلى قِيَم البروفايل المَحفوظَة). تَستَخدِمه
///         واجِهَة عَرض البروفايل مَع <c>AcAttrGrid</c>.</item>
///   <item><c>POST /profile/me/attributes</c> — يَستَقبِل dictionary قِيَم
///         خام، يَدمِجها مَع القالَب عَبر <c>BuildSnapshot</c>، يَحفَظ JSON
///         في <c>ProfileEntity.AttributesJson</c>. يُرجِع snapshots الجَديدَة.</item>
/// </list>
///
/// <para>الأَمان: الـ values الَّتي يُرسِلها المُستَخدِم تُمَرَّر فَقَط
/// لِحُقول مُعَرَّفَة في القالَب (BuildSnapshot يُهمِل المَفاتيح غَير
/// المَعروفَة). الـ labels/أَيقونات تَأتي مِن القالَب الَّذي يَتَحَكَّم بِه
/// المُطَوِّر — لا XSS surface.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class ProfileAttributesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    public ProfileAttributesController(AshareV3DbContext db) => _db = db;

    private string? CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("/profile/attribute-template")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTemplate(CancellationToken ct)
    {
        var template = await ResolveTemplateAsync(ct);
        return this.OkEnvelope("profile.attribute_template", template);
    }

    [HttpGet("/profile/me/attributes")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        var profile = await _db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == CallerId, ct);
        if (profile is null) return this.OkEnvelope("profile.attributes", Array.Empty<DynamicAttribute>());

        var template = await ResolveTemplateAsync(ct);
        var snapshot = BuildSnapshot(profile.AttributesJson, template);
        return this.OkEnvelope("profile.attributes", snapshot);
    }

    public sealed record SaveBody(Dictionary<string, object?>? Values);

    [HttpPost("/profile/me/attributes")]
    public async Task<IActionResult> Save([FromBody] SaveBody body, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == CallerId, ct);
        if (profile is null) return this.NotFoundEnvelope("profile_not_found");

        var template = await ResolveTemplateAsync(ct);
        var values   = body.Values ?? new Dictionary<string, object?>();

        // اِبنِ snapshots لِيَتَحَقَّق الـ helper مِن الـ keys المَعروفَة في
        // القالَب فَقَط (يُهمِل غَيرها) ⇒ سَطر دِفاع ضِدّ keys مَجهولَة.
        var snapshot = DynamicAttributeHelper.BuildSnapshot(template, values);

        // نَحفَظ كائِن مُسَطَّح <c>{ key: rawValue }</c> (لا snapshot كامِل) —
        // أَخَفّ، ومُتَوافِق مَع شَكل V2 القَديم. نُعيد البِناء عِند القِراءَة.
        var flat = snapshot
            .Where(a => a.Value is not null)
            .ToDictionary(a => a.Key, a => a.Value);
        profile.AttributesJson = JsonSerializer.Serialize(flat,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return this.OkEnvelope("profile.attributes", snapshot);
    }

    // ── helpers ──────────────────────────────────────────────────────────
    private async Task<AttributeTemplate> ResolveTemplateAsync(CancellationToken ct)
    {
        var row = await _db.CategoryAttributeTemplates.AsNoTracking()
            .Where(t => t.CategorySlug == "profile")
            .Select(t => t.TemplateJson).FirstOrDefaultAsync(ct);

        AttributeTemplate? template = null;
        if (!string.IsNullOrEmpty(row))
            template = DynamicAttributeHelper.ParseTemplate(row);
        // لا fallback لِكود — لَو DB row غَير مَوجود ⇒ template فارِغ.
        // الإدارَة تَملَأ <c>CategoryAttributeTemplates</c> بِـ Slug="profile"
        // إذا أَرادَت ظُهور حُقول ديناميكِيَّة.
        return template ?? new AttributeTemplate();
    }

    private static List<DynamicAttribute> BuildSnapshot(string? json, AttributeTemplate template)
    {
        var raw = ParseLegacy(json);
        return DynamicAttributeHelper.BuildSnapshot(template, raw);
    }

    private static Dictionary<string, object?> ParseLegacy(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new();
            var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateObject())
                d[p.Name] = Extract(p.Value);
            return d;
        }
        catch { return new(); }
    }

    private static object? Extract(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var i) ? i : (object)el.GetDouble(),
        JsonValueKind.True   => true,
        JsonValueKind.False  => false,
        JsonValueKind.Null   => null,
        JsonValueKind.Array  => el.EnumerateArray().Select(Extract).ToList(),
        JsonValueKind.Object => el.TryGetProperty("value", out var v) ? Extract(v) : el.GetRawText(),
        _ => null,
    };
}
