using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api2.Entities;

namespace Order.Api2.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Category> _repo;

    public CategoriesController(IRepositoryFactory factory)
    {
        _repo = factory.CreateRepository<Category>();
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = await _repo.ListAllAsync(ct);
        var ordered = all.OrderBy(c => c.SortOrder).ToList();
        return this.OkEnvelope("category.list", ordered);
    }
}
