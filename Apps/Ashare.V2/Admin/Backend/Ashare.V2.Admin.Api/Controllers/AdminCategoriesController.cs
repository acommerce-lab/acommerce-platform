using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.V2.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = "AdminOnly")]
public class AdminCategoriesController : ControllerBase
{
    private readonly IBaseAsyncRepository<ProductCategory> _repo;
    private readonly OpEngine _engine;

    public AdminCategoriesController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo   = factory.CreateRepository<ProductCategory>();
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = await _repo.GetAllWithPredicateAsync(c => true);
        return this.OkEnvelope("admin.category.list",
            all.OrderBy(c => c.SortOrder).Select(c => new
            {
                id        = c.Id,
                name      = c.Name,
                icon      = c.Icon,
                parentId  = c.ParentId,
                sortOrder = c.SortOrder
            }));
    }

    public record CreateCategoryDto(string Name, string? Icon, Guid? ParentId, int SortOrder);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto, CancellationToken ct)
    {
        var category = new ProductCategory
        {
            Id        = Guid.NewGuid(),
            Name      = dto.Name,
            Icon      = dto.Icon,
            ParentId  = dto.ParentId,
            SortOrder = dto.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        var op = Entry.Create("admin.category.create")
            .Describe($"Admin creates category: {dto.Name}")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"Category:{category.Id}", 1, ("role", "category"))
            .Tag("name", dto.Name)
            .Execute(async ctx => await _repo.AddAsync(category, ctx.CancellationToken))
            .Build();

        var res = await _engine.ExecuteAsync(op, ct);
        if (!res.Success) return this.BadRequestEnvelope("create_failed", res.ErrorMessage);
        return this.OkEnvelope("admin.category.create", new { category.Id, category.Name });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(id, ct);
        if (c == null) return this.NotFoundEnvelope("category_not_found");

        var op = Entry.Create("admin.category.delete")
            .Describe($"Admin deletes Category:{id}")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"Category:{id}", 1, ("role", "category"))
            .Tag("category_id", id.ToString())
            .Execute(async ctx =>
            {
                c.IsDeleted = true;
                c.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(c, ctx.CancellationToken);
            })
            .Build();

        var res = await _engine.ExecuteAsync(op, ct);
        if (!res.Success) return this.BadRequestEnvelope("delete_failed", res.ErrorMessage);
        return this.OkEnvelope("admin.category.delete", new { id });
    }
}
