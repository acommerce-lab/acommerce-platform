using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api.Entities;

namespace Order.Api.Controllers;

[ApiController]
[Route("api/vendors")]
public class VendorsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Vendor> _repo;

    public VendorsController(IRepositoryFactory factory)
    {
        _repo = factory.CreateRepository<Vendor>();
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = await _repo.GetAllWithPredicateAsync(v => v.IsActive);
        var result = all.Select(v => new
        {
            v.Id, v.OwnerId, v.CategoryId, v.Name, v.Slug, v.Description,
            v.City, v.District, v.Phone, v.LogoEmoji, v.CoverEmoji,
            v.Latitude, v.Longitude, v.OpenHours, v.Rating, v.RatingCount
        });
        return this.OkEnvelope("vendor.list", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var v = await _repo.GetByIdAsync(id, ct);
        return v == null
            ? this.NotFoundEnvelope("vendor_not_found")
            : this.OkEnvelope("vendor.get", v);
    }
}
