using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Api.Entities;

namespace Order.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/vendors")]
[Authorize(Policy = "AdminOnly")]
public class AdminVendorsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Vendor> _vendors;
    private readonly OpEngine _engine;

    public AdminVendorsController(IRepositoryFactory repo, OpEngine engine)
    {
        _vendors = repo.CreateRepository<Vendor>();
        _engine  = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = (await _vendors.ListAllAsync(ct))
            .Where(v => !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new
            {
                v.Id, v.Name, v.City, v.Phone, v.IsActive,
                v.Rating, v.RatingCount, v.LogoEmoji, v.CreatedAt
            }).ToList();
        return this.OkEnvelope("admin.vendors.list", all);
    }

    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var vendor = await _vendors.GetByIdAsync(id, ct);
        if (vendor is null) return this.NotFoundEnvelope("vendor_not_found");

        var op = Entry.Create("admin.vendor.suspend")
            .Describe($"Admin suspends Vendor:{id}")
            .From("User:admin", 1, ("role", "admin"))
            .To($"Vendor:{id}", 1, ("role", "vendor"))
            .Execute(async ctx => { vendor.IsActive = false; await _vendors.UpdateAsync(vendor, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("suspend_failed", result.ErrorMessage);
        return this.OkEnvelope("admin.vendor.suspend", new { vendorId = id, suspended = true });
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var vendor = await _vendors.GetByIdAsync(id, ct);
        if (vendor is null) return this.NotFoundEnvelope("vendor_not_found");

        var op = Entry.Create("admin.vendor.activate")
            .Describe($"Admin activates Vendor:{id}")
            .From("User:admin", 1, ("role", "admin"))
            .To($"Vendor:{id}", 1, ("role", "vendor"))
            .Execute(async ctx => { vendor.IsActive = true; await _vendors.UpdateAsync(vendor, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("activate_failed", result.ErrorMessage);
        return this.OkEnvelope("admin.vendor.activate", new { vendorId = id, active = true });
    }
}
