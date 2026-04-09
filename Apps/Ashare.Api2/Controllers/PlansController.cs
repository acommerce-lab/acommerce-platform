using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api2.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api2.Controllers;

[ApiController]
[Route("api/plans")]
public class PlansController : ControllerBase
{
    private readonly IBaseAsyncRepository<Plan> _repo;

    public PlansController(IRepositoryFactory factory)
    {
        _repo = factory.CreateRepository<Plan>();
    }

    [HttpGet]
    public async Task<IActionResult> ListAll(CancellationToken ct)
    {
        var all = await _repo.GetAllWithPredicateAsync(p => p.IsActive);
        return this.OkEnvelope("plan.list", all.OrderBy(p => p.SortOrder).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(id, ct);
        return p == null ? this.NotFoundEnvelope("plan_not_found") : this.OkEnvelope("plan.get", p);
    }

    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var matches = await _repo.GetAllWithPredicateAsync(p => p.Slug == slug);
        return matches.Count > 0
            ? this.OkEnvelope("plan.get", matches[0])
            : this.NotFoundEnvelope("plan_not_found");
    }

    [HttpGet("business")]
    public async Task<IActionResult> Business(CancellationToken ct)
    {
        var all = await _repo.GetAllWithPredicateAsync(p => p.IsActive && p.Slug.StartsWith("business"));
        return this.OkEnvelope("plan.list.business", all.OrderBy(p => p.SortOrder).ToList());
    }

    [HttpGet("individual")]
    public async Task<IActionResult> Individual(CancellationToken ct)
    {
        var all = await _repo.GetAllWithPredicateAsync(p => p.IsActive && p.Slug.StartsWith("individual"));
        return this.OkEnvelope("plan.list.individual", all.OrderBy(p => p.SortOrder).ToList());
    }

    [HttpGet("special")]
    public async Task<IActionResult> Special(CancellationToken ct)
    {
        var slugs = new[] { "partner-seeker", "commercial-admin", "platform-contract" };
        var all = await _repo.GetAllWithPredicateAsync(p => p.IsActive && slugs.Contains(p.Slug));
        return this.OkEnvelope("plan.list.special", all.OrderBy(p => p.SortOrder).ToList());
    }
}
