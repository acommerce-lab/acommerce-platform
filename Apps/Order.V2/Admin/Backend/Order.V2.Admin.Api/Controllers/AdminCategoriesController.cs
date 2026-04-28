using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Domain;

namespace Order.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = "AdminOnly")]
public class AdminCategoriesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Category> _cats;
    private readonly OpEngine _engine;

    public AdminCategoriesController(IRepositoryFactory repo, OpEngine engine)
    {
        _cats   = repo.CreateRepository<Category>();
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = (await _cats.ListAllAsync(ct))
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.NameAr, c.NameEn, c.Icon, c.Slug })
            .ToList();
        return this.OkEnvelope("admin.categories.list", all);
    }

    public record CreateCategoryBody(string NameAr, string NameEn, string Slug, string? Icon);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryBody req, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var cat = new Category { Id = id, CreatedAt = DateTime.UtcNow,
            NameAr = req.NameAr.Trim(), NameEn = req.NameEn.Trim(),
            Slug = req.Slug.Trim(), Icon = req.Icon ?? "🍽️" };

        var op = Entry.Create("admin.category.create")
            .Describe($"Admin creates Category:{id} ({req.NameAr})")
            .From("User:admin", 1, ("role", "admin"))
            .To($"Category:{id}", 1, ("role", "category"))
            .Tag("name_ar", req.NameAr)
            .Analyze(new RequiredFieldAnalyzer("name_ar", () => req.NameAr))
            .Execute(async ctx => await _cats.AddAsync(cat, ctx.CancellationToken))
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("create_failed", result.ErrorMessage);
        return this.OkEnvelope("admin.category.create", new { id, cat.NameAr, cat.NameEn, cat.Icon });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var cat = await _cats.GetByIdAsync(id, ct);
        if (cat is null) return this.NotFoundEnvelope("category_not_found");

        var op = Entry.Create("admin.category.delete")
            .Describe($"Admin deletes Category:{id}")
            .From("User:admin", 1, ("role", "admin"))
            .To($"Category:{id}", 1, ("role", "category"))
            .Execute(async ctx => { cat.IsDeleted = true; await _cats.UpdateAsync(cat, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("delete_failed", result.ErrorMessage);
        return this.OkEnvelope("admin.category.delete", new { id, deleted = true });
    }
}
