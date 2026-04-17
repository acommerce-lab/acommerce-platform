using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Order.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Order.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/vendors")]
[Authorize(Policy = "AdminOnly")]
public class AdminVendorsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Vendor> _repo;
    private readonly OpEngine _engine;

    public AdminVendorsController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo   = factory.CreateRepository<Vendor>();
        _engine = engine;
    }

    /// <summary>
    /// GET /api/admin/vendors?search=&amp;city=&amp;page=1&amp;pageSize=20
    /// قائمة التجار مع فلترة وترقيم.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? city,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // Frontend expects a flat list — return `.Items` directly.
        var result = await _repo.GetPagedAsync(
            pageNumber: page,
            pageSize: pageSize,
            predicate: v =>
                (search == null || v.Name.Contains(search) || v.Slug.Contains(search)) &&
                (city == null || v.City == city),
            orderBy: v => v.CreatedAt,
            ascending: false);

        var rows = result.Items.Select(v => new
        {
            id           = v.Id,
            storeName    = v.Name,
            ownerName    = (string?)null,
            phone        = (string?)null,
            status       = v.IsActive ? "active" : "suspended",
            category     = v.City,
            orderCount   = 0,
            totalRevenue = 0m,
            createdAt    = v.CreatedAt
        }).ToList();

        return this.OkEnvelope("admin.vendor.list", rows);
    }

    /// <summary>
    /// GET /api/admin/vendors/{id}
    /// تفاصيل تاجر.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var vendor = await _repo.GetByIdAsync(id, ct);
        if (vendor == null) return this.NotFoundEnvelope("vendor_not_found");
        return this.OkEnvelope("admin.vendor.get", vendor);
    }

    /// <summary>
    /// POST /api/admin/vendors/{id}/approve
    /// تفعيل تاجر (تغيير IsActive إلى true).
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var vendor = await _repo.GetByIdAsync(id, ct);
        if (vendor == null) return this.NotFoundEnvelope("vendor_not_found");

        if (vendor.IsActive)
            return this.BadRequestEnvelope("vendor_already_active", "التاجر مفعّل بالفعل");

        var op = Entry.Create("admin.vendor.approve")
            .Describe($"Admin approves Vendor:{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Vendor:{id}", 1, ("role", "vendor"))
            .Tag("vendor_id", id.ToString())
            .Tag("action", "approve")
            .Execute(async ctx =>
            {
                vendor.IsActive = true;
                vendor.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(vendor, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("vendor_approve_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.vendor.approve", new { vendor.Id, vendor.IsActive });
    }

    /// <summary>
    /// POST /api/admin/vendors/{id}/suspend
    /// تعطيل تاجر (تغيير IsActive إلى false).
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var vendor = await _repo.GetByIdAsync(id, ct);
        if (vendor == null) return this.NotFoundEnvelope("vendor_not_found");

        if (!vendor.IsActive)
            return this.BadRequestEnvelope("vendor_already_suspended", "التاجر معطّل بالفعل");

        var op = Entry.Create("admin.vendor.suspend")
            .Describe($"Admin suspends Vendor:{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Vendor:{id}", 1, ("role", "vendor"))
            .Tag("vendor_id", id.ToString())
            .Tag("action", "suspend")
            .Execute(async ctx =>
            {
                vendor.IsActive = false;
                vendor.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(vendor, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("vendor_suspend_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.vendor.suspend", new { vendor.Id, vendor.IsActive });
    }
}
