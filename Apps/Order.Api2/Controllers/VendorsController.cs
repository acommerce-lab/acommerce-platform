using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api2.Entities;

namespace Order.Api2.Controllers;

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
        return this.OkEnvelope("vendor.list", all);
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
