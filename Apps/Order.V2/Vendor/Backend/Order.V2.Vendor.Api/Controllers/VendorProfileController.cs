using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Domain;

namespace Order.V2.Vendor.Api.Controllers;

[ApiController]
[Route("api/vendor/profile")]
[Authorize(Policy = "VendorOnly")]
public class VendorProfileController : ControllerBase
{
    private readonly IBaseAsyncRepository<VendorEntity> _vendors;
    private readonly OpEngine _engine;

    public VendorProfileController(IRepositoryFactory repo, OpEngine engine)
    {
        _vendors = repo.CreateRepository<VendorEntity>();
        _engine  = engine;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);
        var vendor = await _vendors.GetByIdAsync(vendorId, ct);
        if (vendor is null) return this.NotFoundEnvelope("vendor_not_found");

        return this.OkEnvelope("vendor.profile.read", new
        {
            vendor.Id, vendor.Name, vendor.Slug, vendor.Description,
            vendor.City, vendor.District, vendor.Phone,
            vendor.LogoEmoji, vendor.CoverEmoji, vendor.OpenHours,
            vendor.IsActive, vendor.Rating, vendor.RatingCount, vendor.CreatedAt
        });
    }

    public record UpdateProfileBody(string? Name, string? Description,
                                     string? City, string? District,
                                     string? Phone, string? LogoEmoji,
                                     string? CoverEmoji, string? OpenHours);

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileBody req, CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);
        var vendor = await _vendors.GetByIdAsync(vendorId, ct);
        if (vendor is null) return this.NotFoundEnvelope("vendor_not_found");

        var op = Entry.Create("vendor.profile.update")
            .Describe($"Vendor:{vendorId} updates profile")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"Vendor:{vendorId}", 1, ("role", "self"))
            .Execute(async ctx =>
            {
                if (req.Name        is not null) vendor.Name        = req.Name.Trim();
                if (req.Description is not null) vendor.Description = req.Description.Trim();
                if (req.City        is not null) vendor.City        = req.City.Trim();
                if (req.District    is not null) vendor.District    = req.District.Trim();
                if (req.Phone       is not null) vendor.Phone       = req.Phone.Trim();
                if (req.LogoEmoji   is not null) vendor.LogoEmoji   = req.LogoEmoji.Trim();
                if (req.CoverEmoji  is not null) vendor.CoverEmoji  = req.CoverEmoji.Trim();
                if (req.OpenHours   is not null) vendor.OpenHours   = req.OpenHours.Trim();
                vendor.UpdatedAt = DateTime.UtcNow;
                await _vendors.UpdateAsync(vendor, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("update_failed", result.ErrorMessage);
        return this.OkEnvelope("vendor.profile.update", new { vendorId, updated = true });
    }
}
