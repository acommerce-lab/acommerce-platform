using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api.Controllers;

[ApiController]
[Route("api/device-tokens")]
public class DeviceTokensController : ControllerBase
{
    private readonly IBaseAsyncRepository<DeviceToken> _repo;
    private readonly OpEngine _engine;

    public DeviceTokensController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo = factory.CreateRepository<DeviceToken>();
        _engine = engine;
    }

    public record RegisterTokenRequest(Guid UserId, string Token, string Platform);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterTokenRequest req, CancellationToken ct)
    {
        var existing = await _repo.GetAllWithPredicateAsync(t => t.Token == req.Token);
        if (existing.Count > 0)
        {
            var t = existing.First();
            t.LastSeenAt = DateTime.UtcNow;
            t.IsActive = true;
            t.UserId = req.UserId;

            var updateOp = Entry.Create("device.register")
                .Describe($"Re-register device token for User:{req.UserId}")
                .From($"User:{req.UserId}", 1, ("role", "owner"))
                .To($"Device:{t.Id}", 1, ("role", "token"))
                .Tag("platform", req.Platform)
                .Tag("action", "update")
                .Execute(async ctx =>
                {
                    await _repo.UpdateAsync(t, ctx.CancellationToken);
                })
                .Build();

            var updateResult = await _engine.ExecuteAsync(updateOp, ct);
            if (!updateResult.Success) return this.BadRequestEnvelope("device_register_failed", updateResult.ErrorMessage);

            return this.OkEnvelope("device_token.update", t);
        }

        var entity = new DeviceToken
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = req.UserId,
            Token = req.Token,
            Platform = req.Platform,
            LastSeenAt = DateTime.UtcNow
        };

        var op = Entry.Create("device.register")
            .Describe($"Register new device token for User:{req.UserId}")
            .From($"User:{req.UserId}", 1, ("role", "owner"))
            .To($"Device:{entity.Id}", 1, ("role", "token"))
            .Tag("platform", req.Platform)
            .Tag("action", "create")
            .Execute(async ctx =>
            {
                await _repo.AddAsync(entity, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("device_register_failed", result.ErrorMessage);

        return this.OkEnvelope("device_token.register", entity);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetByUser(Guid userId, CancellationToken ct)
    {
        var list = await _repo.GetAllWithPredicateAsync(t => t.UserId == userId && t.IsActive);
        return this.OkEnvelope("device_token.list", list.ToList());
    }

    [HttpDelete("{token}")]
    public async Task<IActionResult> Unregister(string token, CancellationToken ct)
    {
        var matches = await _repo.GetAllWithPredicateAsync(t => t.Token == token);

        var op = Entry.Create("device.unregister")
            .Describe($"Unregister device token ({matches.Count} match(es))")
            .From("System", matches.Count, ("role", "system"))
            .To($"Token:{token[..Math.Min(token.Length, 8)]}", matches.Count, ("role", "token"))
            .Tag("token_count", matches.Count.ToString())
            .Execute(async ctx =>
            {
                foreach (var t in matches)
                {
                    t.IsActive = false;
                    await _repo.UpdateAsync(t, ctx.CancellationToken);
                }
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("device_unregister_failed", result.ErrorMessage);

        return this.NoContentEnvelope("device_token.unregister");
    }
}
