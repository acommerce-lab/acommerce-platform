using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = "AdminOnly")]
public class AdminCategoriesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Category> _repo;
    private readonly OpEngine _engine;

    public AdminCategoriesController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo   = factory.CreateRepository<Category>();
        _engine = engine;
    }

    /// <summary>
    /// GET /api/admin/categories
    /// قائمة جميع الفئات.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var categories = await _repo.ListAllAsync(ct);
        return this.OkEnvelope("admin.category.list", categories.OrderBy(c => c.SortOrder).ToList());
    }

    /// <summary>
    /// POST /api/admin/categories
    /// إنشاء فئة جديدة.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        var category = new Category
        {
            Id        = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Slug      = req.Slug,
            NameAr    = req.NameAr,
            NameEn    = req.NameEn,
            Description = req.Description,
            Icon        = req.Icon,
            SortOrder   = req.SortOrder ?? 0,
            IsActive    = req.IsActive ?? true
        };

        var op = Entry.Create("admin.category.create")
            .Describe($"Admin creates category '{category.NameAr}' (slug: {category.Slug})")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Category:{category.Id}", 1, ("role", "category"))
            .Tag("category_id", category.Id.ToString())
            .Tag("category_slug", category.Slug)
            .Analyze(new RequiredFieldAnalyzer("nameAr", () => req.NameAr))
            .Analyze(new RequiredFieldAnalyzer("nameEn", () => req.NameEn))
            .Analyze(new RequiredFieldAnalyzer("slug", () => req.Slug))
            .Execute(async ctx =>
            {
                await _repo.AddAsync(category, ctx.CancellationToken);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, category, ct);
        if (envelope.Operation.Status != "Success")
            return this.BadRequestEnvelope("category_create_failed", envelope.Operation.ErrorMessage);

        return Created($"/api/admin/categories/{category.Id}", envelope);
    }

    /// <summary>
    /// PUT /api/admin/categories/{id}
    /// تحديث فئة.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest req, CancellationToken ct)
    {
        var category = await _repo.GetByIdAsync(id, ct);
        if (category == null) return this.NotFoundEnvelope("category_not_found");

        var op = Entry.Create("admin.category.update")
            .Describe($"Admin updates category #{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Category:{id}", 1, ("role", "category"))
            .Tag("category_id", id.ToString())
            .Execute(async ctx =>
            {
                if (req.NameAr != null)      category.NameAr = req.NameAr;
                if (req.NameEn != null)      category.NameEn = req.NameEn;
                if (req.Description != null) category.Description = req.Description;
                if (req.Icon != null)        category.Icon = req.Icon;
                if (req.SortOrder.HasValue)  category.SortOrder = req.SortOrder.Value;
                if (req.IsActive.HasValue)   category.IsActive = req.IsActive.Value;
                category.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(category, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("category_update_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.category.update", category);
    }

    /// <summary>
    /// DELETE /api/admin/categories/{id}
    /// حذف ناعم لفئة.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var category = await _repo.GetByIdAsync(id, ct);
        if (category == null) return this.NotFoundEnvelope("category_not_found");

        var op = Entry.Create("admin.category.delete")
            .Describe($"Admin soft-deletes category #{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"System:archive", 1, ("role", "archive"))
            .Tag("category_id", id.ToString())
            .Execute(async ctx =>
            {
                await _repo.SoftDeleteAsync(id, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("category_delete_failed", result.ErrorMessage);

        return this.NoContentEnvelope("admin.category.delete");
    }

    public record CreateCategoryRequest(
        string Slug,
        string NameAr,
        string NameEn,
        string? Description,
        string? Icon,
        int? SortOrder,
        bool? IsActive);

    public record UpdateCategoryRequest(
        string? NameAr,
        string? NameEn,
        string? Description,
        string? Icon,
        int? SortOrder,
        bool? IsActive);
}
