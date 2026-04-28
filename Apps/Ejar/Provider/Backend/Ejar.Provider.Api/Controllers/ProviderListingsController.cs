using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Ejar.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ejar.Provider.Api.Controllers;

/// <summary>
/// Provider's listings: every route filters to the caller's own listings (OwnerId).
/// Domain entity (<c>EjarSeed.ListingSeed</c>) lives in the Customer assembly and
/// is mutated through the shared in-memory list — both backends see the same data.
/// </summary>
[ApiController]
[Authorize]
public class ProviderListingsController : ControllerBase
{
    private readonly OpEngine _engine;
    public ProviderListingsController(OpEngine engine) => _engine = engine;

    private string CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("user_id missing");

    // ─── GET /my-listings ──────────────────────────────────────────────────
    [HttpGet("/my-listings")]
    public IActionResult MyListings()
    {
        var me = CallerId;
        var rows = EjarSeed.Listings
            .Where(l => l.OwnerId == me)
            .OrderByDescending(l => l.IsVerified)
            .Select(MapRow)
            .ToList();
        return this.OkEnvelope("listing.list", rows);
    }

    // ─── POST /my-listings ─────────────────────────────────────────────────
    public sealed record CreateListingRequest(
        string? Title, string? Description, decimal Price, string? TimeUnit,
        string? PropertyType, string? City, string? District,
        double Lat, double Lng, IReadOnlyList<string>? Amenities,
        int BedroomCount, int BathroomCount, int AreaSqm,
        IReadOnlyList<string>? Images);

    [HttpPost("/my-listings")]
    public async Task<IActionResult> Create([FromBody] CreateListingRequest req, CancellationToken ct)
    {
        var me = CallerId;
        var listing = new EjarSeed.ListingSeed(
            Id: $"L-{Guid.NewGuid():N}".Substring(0, 8),
            Title: req.Title ?? "",
            Description: req.Description ?? "",
            Price: req.Price,
            TimeUnit: req.TimeUnit ?? "monthly",
            PropertyType: req.PropertyType ?? "apartment",
            City: req.City ?? "",
            District: req.District ?? "",
            Lat: req.Lat, Lng: req.Lng,
            Amenities: req.Amenities ?? Array.Empty<string>(),
            OwnerId: me,
            BedroomCount: req.BedroomCount,
            BathroomCount: req.BathroomCount,
            AreaSqm: req.AreaSqm,
            Images: req.Images);

        var op = Entry.Create("listing.create")
            .Describe($"Provider {me} publishes listing {listing.Id}")
            .From($"Provider:{me}", 1, ("role", "owner"))
            .To($"Listing:{listing.Id}", 1, ("role", "created"))
            .Tag("city", listing.City).Tag("type", listing.PropertyType)
            .Analyze(new RequiredFieldAnalyzer("title", () => listing.Title))
            .Analyze(new RequiredFieldAnalyzer("city",  () => listing.City))
            .Analyze(new ConditionAnalyzer("price_positive", _ => listing.Price > 0, "السعر يجب أن يكون أكبر من صفر"))
            .Execute(ctx => { EjarSeed.Listings.Add(listing); return Task.CompletedTask; })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, MapRow(listing), ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "listing_create_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.create", MapRow(listing));
    }

    // ─── DELETE /my-listings/{id} ──────────────────────────────────────────
    [HttpDelete("/my-listings/{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var me = CallerId;
        var ix = EjarSeed.Listings.FindIndex(l => l.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("listing_not_found");
        if (EjarSeed.Listings[ix].OwnerId != me)
            return this.ForbiddenEnvelope("not_owner", "ليس لديك صلاحية حذف هذا الإعلان");

        var op = Entry.Create("listing.delete")
            .Describe($"Provider {me} deletes listing {id}")
            .From($"Provider:{me}", -1, ("role", "owner"))
            .To($"Listing:{id}", 0, ("role", "removed"))
            .Tag("listing_id", id)
            .Execute(ctx => { EjarSeed.Listings.RemoveAt(ix); return Task.CompletedTask; })
            .Build();
        var env = await _engine.ExecuteEnvelopeAsync(op, new { id }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "listing_delete_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.delete", new { id });
    }

    // ─── helpers ──────────────────────────────────────────────────────────
    private static object MapRow(EjarSeed.ListingSeed l) => new {
        l.Id, l.Title, l.Description, l.Price, l.TimeUnit, l.PropertyType,
        l.City, l.District, l.Lat, l.Lng, l.Amenities,
        l.BedroomCount, l.BathroomCount, l.AreaSqm,
        l.IsVerified, l.ViewsCount, l.Status, l.Images
    };
}
