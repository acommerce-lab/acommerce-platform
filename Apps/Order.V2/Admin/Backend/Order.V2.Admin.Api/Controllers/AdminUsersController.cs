using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Domain;

namespace Order.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdminOnly")]
public class AdminUsersController : ControllerBase
{
    private readonly IBaseAsyncRepository<User> _users;
    private readonly OpEngine _engine;

    public AdminUsersController(IRepositoryFactory repo, OpEngine engine)
    {
        _users  = repo.CreateRepository<User>();
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = (await _users.ListAllAsync(ct))
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id, u.FullName, u.PhoneNumber, u.Email,
                u.Role, u.IsActive, u.CreatedAt
            }).ToList();
        return this.OkEnvelope("admin.users.list", all);
    }

    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null) return this.NotFoundEnvelope("user_not_found");

        var op = Entry.Create("admin.user.suspend")
            .Describe($"Admin suspends User:{id}")
            .From("User:admin", 1, ("role", "admin"))
            .To($"User:{id}", 1, ("role", "target"))
            .Tag("user_id", id.ToString())
            .Execute(async ctx =>
            {
                user.IsActive = false;
                await _users.UpdateAsync(user, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("suspend_failed", result.ErrorMessage);
        return this.OkEnvelope("admin.user.suspend", new { userId = id, suspended = true });
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null) return this.NotFoundEnvelope("user_not_found");

        var op = Entry.Create("admin.user.activate")
            .Describe($"Admin activates User:{id}")
            .From("User:admin", 1, ("role", "admin"))
            .To($"User:{id}", 1, ("role", "target"))
            .Tag("user_id", id.ToString())
            .Execute(async ctx =>
            {
                user.IsActive = true;
                await _users.UpdateAsync(user, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("activate_failed", result.ErrorMessage);
        return this.OkEnvelope("admin.user.activate", new { userId = id, active = true });
    }
}
