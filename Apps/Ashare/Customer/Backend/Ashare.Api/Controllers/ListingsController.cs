using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.DynamicAttributes;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using ACommerce.Subscriptions.Operations;
using Ashare.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Listing> _repo;
    private readonly IRepositoryFactory _factory;
    private readonly OpEngine _engine;

    public ListingsController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo = factory.CreateRepository<Listing>();
        _factory = factory;
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? city,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _repo.GetPagedAsync(
            pageNumber: page,
            pageSize: pageSize,
            predicate: l =>
                l.Status == ListingStatus.Published &&
                (categoryId == null || l.CategoryId == categoryId) &&
                (city == null || l.City == city) &&
                (minPrice == null || l.Price >= minPrice) &&
                (maxPrice == null || l.Price <= maxPrice),
            orderBy: l => l.PublishedAt!,
            ascending: false);

        return this.OkEnvelope("listing.list", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        var viewOp = Entry.Create("listing.view")
            .Describe($"View listing {id}")
            .From("Viewer:anonymous", 1, ("role", "viewer"))
            .To($"Listing:{id}", 1, ("role", "listing"))
            .Tag("listing_id", id.ToString())
            .Execute(async ctx =>
            {
                listing.ViewCount++;
                await _repo.UpdateAsync(listing, ctx.CancellationToken);
            })
            .Build();

        await _engine.ExecuteAsync(viewOp, ct);

        return this.OkEnvelope("listing.get", listing);
    }

    public record CreateListingRequest(
        Guid OwnerId,
        Guid CategoryId,
        string Title,
        string Description,
        decimal Price,
        int Duration,
        string TimeUnit,
        string City,
        string? District,
        double? Latitude,
        double? Longitude,
        string? LicenseNumber,
        Dictionary<string, object?>? Attributes);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateListingRequest req, CancellationToken ct)
    {
        var catRepo = _factory.CreateRepository<Category>();
        var category = await catRepo.GetByIdAsync(req.CategoryId, ct);
        if (category == null) return this.NotFoundEnvelope("category_not_found");

        var template = DynamicAttributeHelper.ParseTemplate(category.AttributeTemplateJson) ?? new AttributeTemplate();
        var snapshot = DynamicAttributeHelper.BuildSnapshot(template, req.Attributes ?? new());

        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OwnerId = req.OwnerId,
            CategoryId = req.CategoryId,
            Title = req.Title,
            Description = req.Description,
            Price = req.Price,
            Duration = req.Duration,
            TimeUnit = req.TimeUnit,
            City = req.City,
            District = req.District,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            LicenseNumber = req.LicenseNumber,
            DynamicAttributesJson = DynamicAttributeHelper.SerializeAttributes(snapshot),
            Status = ListingStatus.Draft
        };

        var op = Entry.Create(AshareOps.ListingCreate)
            .Describe($"Owner:{req.OwnerId} creates listing in Category:{req.CategoryId}")
            .From($"User:{req.OwnerId}", 1, ("role", AshareRoles.Owner.Name))
            .To($"Category:{req.CategoryId}", 1, ("role", AshareRoles.Category.Name))
            .Tag(AshareTags.ListingId, listing.Id)
            .Tag(AshareTags.CategoryId, req.CategoryId)
            .Tag(QuotaTagKeys.Check, QuotaCheckKinds.ListingsCreate)
            .Tag(QuotaTagKeys.UserId, req.OwnerId)
            .Tag(QuotaTagKeys.ScopeKey, "listing_categories")
            .Tag(QuotaTagKeys.ScopeValue, category.Slug)
            .Execute(async ctx =>
            {
                ctx.TryGet<Ashare.Api.Entities.Subscription>("linked_subscription", out var sub);
                ctx.TryGet<Ashare.Api.Entities.Plan>("linked_plan", out var plan);

                listing.Status = ListingStatus.Published;
                listing.PublishedAt = DateTime.UtcNow;
                if (sub != null)
                {
                    listing.SubscriptionId = sub.Id;
                    listing.PlanIdSnapshot = plan?.Id;
                    listing.BillingPeriodStart = sub.StartDate;
                    listing.BillingPeriodEnd = sub.EndDate;
                }
                await _repo.AddAsync(listing, ctx.CancellationToken);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, listing, ct);

        if (envelope.Operation.Status != "Success")
        {
            envelope.Error = new OperationError
            {
                Code = "listing_create_blocked",
                Message = envelope.Operation.ErrorMessage,
                Hint = "اشترك في باقة تدعم هذه الفئة من /api/plans"
            };
            return StatusCode(403, envelope);
        }

        return Created($"/api/listings/{listing.Id}", envelope);
    }

    [HttpGet("by-owner/{ownerId:guid}")]
    public async Task<IActionResult> ByOwner(Guid ownerId, CancellationToken ct)
    {
        var list = await _repo.GetAllWithPredicateAsync(l => l.OwnerId == ownerId);
        return this.OkEnvelope("listing.list.by_owner",
            list.OrderByDescending(l => l.CreatedAt).ToList());
    }

    public record UpdateListingRequest(
        string? Title,
        string? Description,
        decimal? Price,
        int? Duration,
        string? TimeUnit,
        string? City,
        string? District,
        double? Latitude,
        double? Longitude,
        string? LicenseNumber,
        string? ImagesCsv,
        Dictionary<string, object?>? Attributes);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateListingRequest req, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("listing.update")
            .Describe($"Owner:{listing.OwnerId} updates listing #{id}")
            .From($"User:{listing.OwnerId}", 1, ("role", "owner"))
            .To($"Listing:{id}", 1, ("role", "listing"))
            .Tag("listing_id", id.ToString())
            .Execute(async ctx =>
            {
                if (req.Title != null) listing.Title = req.Title;
                if (req.Description != null) listing.Description = req.Description;
                if (req.Price.HasValue) listing.Price = req.Price.Value;
                if (req.Duration.HasValue) listing.Duration = req.Duration.Value;
                if (req.TimeUnit != null) listing.TimeUnit = req.TimeUnit;
                if (req.City != null) listing.City = req.City;
                if (req.District != null) listing.District = req.District;
                if (req.Latitude.HasValue) listing.Latitude = req.Latitude;
                if (req.Longitude.HasValue) listing.Longitude = req.Longitude;
                if (req.LicenseNumber != null) listing.LicenseNumber = req.LicenseNumber;
                if (req.ImagesCsv != null) listing.ImagesCsv = req.ImagesCsv;

                if (req.Attributes != null)
                {
                    var catRepo = _factory.CreateRepository<Category>();
                    var category = await catRepo.GetByIdAsync(listing.CategoryId, ctx.CancellationToken);
                    var template = DynamicAttributeHelper.ParseTemplate(category?.AttributeTemplateJson) ?? new AttributeTemplate();
                    var snapshot = DynamicAttributeHelper.BuildSnapshot(template, req.Attributes);
                    listing.DynamicAttributesJson = DynamicAttributeHelper.SerializeAttributes(snapshot);
                }

                listing.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(listing, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("listing_update_failed", result.ErrorMessage);

        return this.OkEnvelope("listing.update", listing);
    }

    [HttpPost("{id:guid}/feature")]
    public async Task<IActionResult> Feature(Guid id, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("listing.feature")
            .Describe($"Toggle featured for listing #{id} (Owner:{listing.OwnerId})")
            .From($"User:{listing.OwnerId}", 1, ("role", "owner"))
            .To($"Listing:{id}", 1, ("role", "listing"))
            .Tag("listing_id", id.ToString())
            .Execute(async ctx =>
            {
                listing.IsFeatured = !listing.IsFeatured;
                listing.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(listing, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("listing_feature_failed", result.ErrorMessage);

        return this.OkEnvelope("listing.feature", new { listing.Id, listing.IsFeatured });
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("listing.publish")
            .Describe($"Publish listing #{listing.Id} by Owner:{listing.OwnerId}")
            .From($"Owner:{listing.OwnerId}", 1, ("role", "owner"))
            .To($"Listing:{listing.Id}", 1, ("role", "listing"))
            .Tag("listing_id", listing.Id.ToString())
            .Tag("owner_id", listing.OwnerId.ToString())
            .Execute(async ctx =>
            {
                listing.Status = ListingStatus.Published;
                listing.PublishedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(listing, ctx.CancellationToken);
                ctx.Set("listingId", listing.Id);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("listing_publish_failed", result.ErrorMessage);

        return this.OkEnvelope("listing.publish", listing);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var op = Entry.Create("listing.delete")
            .Describe($"Soft-delete listing #{id}")
            .From($"Listing:{id}", 1, ("role", "listing"))
            .To($"System:archive", 1, ("role", "archive"))
            .Tag("listing_id", id.ToString())
            .Execute(async ctx =>
            {
                await _repo.SoftDeleteAsync(id, ctx.CancellationToken);
                ctx.Set("listingId", id);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("listing_delete_failed", result.ErrorMessage);

        return this.NoContentEnvelope("listing.delete");
    }
}
