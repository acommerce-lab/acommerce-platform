using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Order.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Order.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdminOnly")]
public class AdminUsersController : ControllerBase
{
    private readonly IBaseAsyncRepository<User> _repo;
    private readonly OpEngine _engine;

    public AdminUsersController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo   = factory.CreateRepository<User>();
        _engine = engine;
    }

    /// <summary>
    /// GET /api/admin/users?search=&amp;page=1&amp;pageSize=20
    /// قائمة المستخدمين مع دعم البحث والترقيم.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _repo.GetPagedAsync(
            pageNumber: page,
            pageSize: pageSize,
            predicate: u =>
                (search == null ||
                 (u.FullName != null && u.FullName.Contains(search)) ||
                 u.PhoneNumber.Contains(search) ||
                 (u.Email != null && u.Email.Contains(search))),
            orderBy: u => u.CreatedAt,
            ascending: false);

        return this.OkEnvelope("admin.user.list", result);
    }

    /// <summary>
    /// GET /api/admin/users/{id}
    /// تفاصيل مستخدم.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(id, ct);
        if (user == null) return this.NotFoundEnvelope("user_not_found");
        return this.OkEnvelope("admin.user.get", user);
    }

    /// <summary>
    /// POST /api/admin/users/{id}/suspend
    /// تعطيل حساب مستخدم.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(id, ct);
        if (user == null) return this.NotFoundEnvelope("user_not_found");

        if (!user.IsActive)
            return this.BadRequestEnvelope("user_already_suspended", "المستخدم معطّل بالفعل");

        var op = Entry.Create("admin.user.suspend")
            .Describe($"Admin suspends User:{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"User:{id}", 1, ("role", "user"))
            .Tag("user_id", id.ToString())
            .Tag("action", "suspend")
            .Execute(async ctx =>
            {
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(user, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("user_suspend_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.user.suspend", new { user.Id, user.IsActive });
    }

    /// <summary>
    /// POST /api/admin/users/{id}/activate
    /// تفعيل حساب مستخدم.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(id, ct);
        if (user == null) return this.NotFoundEnvelope("user_not_found");

        if (user.IsActive)
            return this.BadRequestEnvelope("user_already_active", "المستخدم مفعّل بالفعل");

        var op = Entry.Create("admin.user.activate")
            .Describe($"Admin activates User:{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"User:{id}", 1, ("role", "user"))
            .Tag("user_id", id.ToString())
            .Tag("action", "activate")
            .Execute(async ctx =>
            {
                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(user, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("user_activate_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.user.activate", new { user.Id, user.IsActive });
    }
}
