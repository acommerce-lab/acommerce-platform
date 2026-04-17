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
[Route("api/admin/plans")]
[Authorize(Policy = "AdminOnly")]
public class AdminPlansController : ControllerBase
{
    private readonly IBaseAsyncRepository<Plan> _repo;
    private readonly OpEngine _engine;

    public AdminPlansController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo   = factory.CreateRepository<Plan>();
        _engine = engine;
    }

    /// <summary>
    /// GET /api/admin/plans
    /// قائمة جميع الباقات.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var plans = await _repo.ListAllAsync(ct);
        return this.OkEnvelope("admin.plan.list", plans.OrderBy(p => p.SortOrder).ToList());
    }

    /// <summary>
    /// POST /api/admin/plans
    /// إنشاء باقة جديدة.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlanRequest req, CancellationToken ct)
    {
        var plan = new Plan
        {
            Id           = Guid.NewGuid(),
            CreatedAt    = DateTime.UtcNow,
            Name         = req.Name,
            NameEn       = req.NameEn,
            Slug         = req.Slug,
            Description  = req.Description,
            MonthlyPrice = req.MonthlyPrice,
            Currency     = req.Currency ?? "SAR",
            MaxListings  = req.MaxListings,
            IsActive     = req.IsActive ?? true,
            SortOrder    = req.SortOrder ?? 0,
            AllowedCategorySlugs = req.AllowedCategorySlugs
        };

        var op = Entry.Create("admin.plan.create")
            .Describe($"Admin creates plan '{plan.Name}' (slug: {plan.Slug})")
            .From($"Admin:system", plan.MonthlyPrice, ("role", "admin"))
            .To($"Plan:{plan.Id}", plan.MonthlyPrice, ("role", "plan"))
            .Tag("plan_id", plan.Id.ToString())
            .Tag("plan_slug", plan.Slug)
            .Analyze(new RequiredFieldAnalyzer("name", () => req.Name))
            .Analyze(new RequiredFieldAnalyzer("slug", () => req.Slug))
            .Execute(async ctx =>
            {
                await _repo.AddAsync(plan, ctx.CancellationToken);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, plan, ct);
        if (envelope.Operation.Status != "Success")
            return this.BadRequestEnvelope("plan_create_failed", envelope.Operation.ErrorMessage);

        return Created($"/api/admin/plans/{plan.Id}", envelope);
    }

    /// <summary>
    /// PUT /api/admin/plans/{id}
    /// تحديث بيانات باقة.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlanRequest req, CancellationToken ct)
    {
        var plan = await _repo.GetByIdAsync(id, ct);
        if (plan == null) return this.NotFoundEnvelope("plan_not_found");

        var op = Entry.Create("admin.plan.update")
            .Describe($"Admin updates plan #{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Plan:{id}", 1, ("role", "plan"))
            .Tag("plan_id", id.ToString())
            .Execute(async ctx =>
            {
                if (req.Name != null)         plan.Name = req.Name;
                if (req.NameEn != null)       plan.NameEn = req.NameEn;
                if (req.Description != null)  plan.Description = req.Description;
                if (req.MonthlyPrice.HasValue) plan.MonthlyPrice = req.MonthlyPrice.Value;
                if (req.MaxListings.HasValue)  plan.MaxListings = req.MaxListings.Value;
                if (req.IsActive.HasValue)     plan.IsActive = req.IsActive.Value;
                if (req.SortOrder.HasValue)    plan.SortOrder = req.SortOrder.Value;
                if (req.AllowedCategorySlugs != null) plan.AllowedCategorySlugs = req.AllowedCategorySlugs;
                plan.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(plan, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("plan_update_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.plan.update", plan);
    }

    /// <summary>
    /// DELETE /api/admin/plans/{id}
    /// تعطيل باقة (حذف ناعم).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var plan = await _repo.GetByIdAsync(id, ct);
        if (plan == null) return this.NotFoundEnvelope("plan_not_found");

        var op = Entry.Create("admin.plan.deactivate")
            .Describe($"Admin deactivates plan #{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"System:archive", 1, ("role", "archive"))
            .Tag("plan_id", id.ToString())
            .Execute(async ctx =>
            {
                plan.IsActive = false;
                plan.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(plan, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("plan_deactivate_failed", result.ErrorMessage);

        return this.NoContentEnvelope("admin.plan.deactivate");
    }

    public record CreatePlanRequest(
        string Name,
        string? NameEn,
        string Slug,
        string? Description,
        decimal MonthlyPrice,
        string? Currency,
        int MaxListings,
        bool? IsActive,
        int? SortOrder,
        string? AllowedCategorySlugs);

    public record UpdatePlanRequest(
        string? Name,
        string? NameEn,
        string? Description,
        decimal? MonthlyPrice,
        int? MaxListings,
        bool? IsActive,
        int? SortOrder,
        string? AllowedCategorySlugs);
}
