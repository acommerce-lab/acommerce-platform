using Microsoft.AspNetCore.Mvc;
using ACommerce.Kits.Support.Domain;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Support.Backend;

/// <summary>
/// متحكم الدعم — يستخدم التاجات القياسية للتواصل مع معترض البيانات.
/// </summary>
[ApiController, Route("api/support")]
public class SupportController : ControllerBase
{
    private readonly OpEngine _engine;

    public SupportController(OpEngine engine)
    {
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var op = Entry.Create("support.ticket.list")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(SupportTicket))
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, ctx => {
            return ctx.Get<IReadOnlyList<SupportTicket>>("db_result");
        });

        return Ok(env);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SupportTicket ticket, CancellationToken ct)
    {
        var op = Entry.Create("support.ticket.create")
            .Tag(OperationTags.DbAction, DataOperationTypes.Create)
            .Tag(OperationTags.TargetEntity, nameof(SupportTicket))
            .Build();
        
        op.Metadata["entity"] = ticket;

        var env = await _engine.ExecuteEnvelopeAsync(op, ctx => {
            return ctx.Get<SupportTicket>("db_result");
        }, ct);

        return Ok(env);
    }
}
