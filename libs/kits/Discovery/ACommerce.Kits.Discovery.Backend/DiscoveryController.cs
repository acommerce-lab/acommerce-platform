using Microsoft.AspNetCore.Mvc;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Discovery.Backend;

/// <summary>
/// متحكم الاستكشاف — يعتمد بالكامل على المعترض العام للبيانات والتاجات
/// القياسية. يكشف ٣ نقاط قراءة عامّة (لا يلزم تسجيل دخول):
///   <c>GET /categories</c>، <c>GET /cities</c>، <c>GET /amenities</c>.
///
/// <para>المسارات بلا prefix لتطابق سوق الـ marketplace التطبيقاتيّ
/// (إيجار، عشير، الخ.). تطبيقات تريد namespace أخرى تكتب controller
/// خاصّاً يستدعي نفس الـ ops.</para>
/// </summary>
[ApiController]
public class DiscoveryController : ControllerBase
{
    private readonly OpEngine _engine;

    public DiscoveryController(OpEngine engine)
    {
        _engine = engine;
    }

    [HttpGet("/categories")]
    public async Task<IActionResult> Categories()
    {
        var op = Entry.Create("discovery.categories.list")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(DiscoveryCategory))
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, ctx => {
            var items = ctx.Get<IReadOnlyList<DiscoveryCategory>>("db_result");
            return items?.Select(c => new {
                id = c.Slug, label = c.Label, icon = c.Icon, kind = c.Kind
            });
        });

        return Ok(env);
    }

    [HttpGet("/cities")]
    public async Task<IActionResult> Cities()
    {
        var op = Entry.Create("discovery.cities.list")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(DiscoveryRegion))
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, ctx => {
            var items = ctx.Get<IReadOnlyList<DiscoveryRegion>>("db_result");
            return items?.Where(r => r.Level == 1).Select(r => r.Name);
        });

        return Ok(env);
    }

    [HttpGet("/amenities")]
    public async Task<IActionResult> Amenities()
    {
        var op = Entry.Create("discovery.amenities.list")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(DiscoveryAmenity))
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, ctx => {
            var items = ctx.Get<IReadOnlyList<DiscoveryAmenity>>("db_result");
            return items?.Select(a => new { key = a.Slug, label = a.Label });
        });

        return Ok(env);
    }
}
