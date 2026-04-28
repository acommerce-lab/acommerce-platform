using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Ejar.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ejar.Admin.Api.Controllers;

/// <summary>
/// Admin view of all listings (regardless of OwnerId) with verify/suspend actions.
/// Mutations go through OAM operations so the audit trail is preserved.
/// </summary>
[ApiController]
[Authorize]
[Route("admin/listings")]
public class AdminListingsController : ControllerBase
{
    private readonly OpEngine _engine;
    public AdminListingsController(OpEngine engine) => _engine = engine;

    [HttpGet]
    public IActionResult List()
    {
        var rows = EjarSeed.Listings
            .OrderByDescending(l => l.IsVerified)
            .Select(l => new {
                l.Id, l.Title, l.City, l.PropertyType, l.Price, l.TimeUnit,
                l.OwnerId, l.IsVerified, l.Status, l.ViewsCount
            }).ToList();
        return this.OkEnvelope("admin.listing.list", rows);
    }

    [HttpPost("{id}/verify")]
    public async Task<IActionResult> Verify(string id, CancellationToken ct)
    {
        var ix = EjarSeed.Listings.FindIndex(l => l.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("admin.listing.verify")
            .Describe($"Admin verifies listing {id}")
            .From("Admin:System", 1, ("role", "verifier"))
            .To($"Listing:{id}", 1, ("role", "verified"))
            .Tag("listing_id", id)
            .Execute(ctx =>
            {
                EjarSeed.Listings[ix] = EjarSeed.Listings[ix] with { IsVerified = true };
                return Task.CompletedTask;
            })
            .Build();
        var env = await _engine.ExecuteEnvelopeAsync(op, new { id, isVerified = true }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope("verify_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("admin.listing.verify", new { id, isVerified = true });
    }

    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> Suspend(string id, CancellationToken ct)
    {
        var ix = EjarSeed.Listings.FindIndex(l => l.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("admin.listing.suspend")
            .Describe($"Admin suspends listing {id}")
            .From("Admin:System", 1, ("role", "moderator"))
            .To($"Listing:{id}", 0, ("role", "suspended"))
            .Tag("listing_id", id)
            .Execute(ctx =>
            {
                EjarSeed.Listings[ix] = EjarSeed.Listings[ix] with { Status = 0 };
                return Task.CompletedTask;
            })
            .Build();
        var env = await _engine.ExecuteEnvelopeAsync(op, new { id, status = 0 }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope("suspend_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("admin.listing.suspend", new { id, status = 0 });
    }
}
