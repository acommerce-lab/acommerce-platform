using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Api.Entities;

namespace Order.V2.Api.Controllers;

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
        return this.OkEnvelope("category.list", all.OrderBy(c => c.SortOrder));
    }
}
