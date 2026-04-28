using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.V2.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdminOnly")]
public class AdminUsersController : ControllerBase
{
    private readonly IBaseAsyncRepository<Profile> _repo;
    private readonly OpEngine _engine;

    public AdminUsersController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo   = factory.CreateRepository<Profile>();
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _repo.GetPagedAsync(
            pageNumber: page, pageSize: pageSize,
            predicate: p => search == null
                || (p.FullName != null && p.FullName.Contains(search))
                || (p.PhoneNumber != null && p.PhoneNumber.Contains(search))
                || (p.NationalId  != null && p.NationalId.Contains(search)),
            orderBy: p => p.CreatedAt, ascending: false);

        var rows = result.Items.Select(p => new
        {
            id          = p.Id,
            fullName    = p.FullName,
            phoneNumber = p.PhoneNumber,
            nationalId  = p.NationalId,
            city        = p.City,
            role        = p.Role,
            isActive    = p.IsActive,
            nafathVerified = p.NafathVerified,
            createdAt   = p.CreatedAt
        });
        return this.OkEnvelope("admin.user.list", rows);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(id, ct);
        if (p == null) return this.NotFoundEnvelope("user_not_found");
        return this.OkEnvelope("admin.user.get", p);
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(id, ct);
        if (p == null) return this.NotFoundEnvelope("user_not_found");
        if (!p.IsActive) return this.BadRequestEnvelope("already_suspended");

        var op = Entry.Create("admin.user.suspend")
            .Describe($"Admin suspends User:{id}")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"User:{id}", 1, ("role", "user"))
            .Tag("user_id", id.ToString())
            .Execute(async ctx =>
            {
                p.IsActive  = false;
                p.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(p, ctx.CancellationToken);
            })
            .Build();

        var res = await _engine.ExecuteAsync(op, ct);
        if (!res.Success) return this.BadRequestEnvelope("suspend_failed", res.ErrorMessage);
        return this.OkEnvelope("admin.user.suspend", new { p.Id, p.IsActive });
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(id, ct);
        if (p == null) return this.NotFoundEnvelope("user_not_found");
        if (p.IsActive) return this.BadRequestEnvelope("already_active");

        var op = Entry.Create("admin.user.activate")
            .Describe($"Admin activates User:{id}")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"User:{id}", 1, ("role", "user"))
            .Tag("user_id", id.ToString())
            .Execute(async ctx =>
            {
                p.IsActive  = true;
                p.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(p, ctx.CancellationToken);
            })
            .Build();

        var res = await _engine.ExecuteAsync(op, ct);
        if (!res.Success) return this.BadRequestEnvelope("activate_failed", res.ErrorMessage);
        return this.OkEnvelope("admin.user.activate", new { p.Id, p.IsActive });
    }
}
