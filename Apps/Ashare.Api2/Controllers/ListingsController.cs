using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api2.Entities;
using ACommerce.Subscriptions.Operations;
using Ashare.Api2.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api2.Controllers;

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

        listing.ViewCount++;
        await _repo.UpdateAsync(listing, ct);

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
        string? PropertyType,
        int? Floor,
        double? Area,
        int? Rooms,
        int? Bathrooms,
        bool? Furnished,
        string? LicenseNumber);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateListingRequest req, CancellationToken ct)
    {
        // نحتاج slug الفئة لمعترض الحصة (للتحقق من النطاق المسموح به)
        var catRepo = _factory.CreateRepository<Category>();
        var category = await catRepo.GetByIdAsync(req.CategoryId, ct);
        if (category == null) return this.NotFoundEnvelope("category_not_found");

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
            PropertyType = req.PropertyType,
            Floor = req.Floor,
            Area = req.Area,
            Rooms = req.Rooms,
            Bathrooms = req.Bathrooms,
            Furnished = req.Furnished,
            LicenseNumber = req.LicenseNumber,
            Status = ListingStatus.Draft
        };

        // === قيد بسيط - الجوانب المتقاطعة محقونة من الـ registry ===
        // القيد يستخدم كتالوجات typed: AshareOps, AshareTags, QuotaTagKeys
        var op = Entry.Create(AshareOps.ListingCreate)
            .Describe($"Owner:{req.OwnerId} creates listing in Category:{req.CategoryId}")
            .From($"User:{req.OwnerId}", 1, ("role", AshareRoles.Owner.Name))
            .To($"Category:{req.CategoryId}", 1, ("role", AshareRoles.Category.Name))
            .Tag(AshareTags.ListingId, listing.Id)
            .Tag(AshareTags.CategoryId, req.CategoryId)
            // ↓↓↓ علامات typed تُفعّل معترضات الاشتراكات تلقائياً:
            .Tag(QuotaTagKeys.Check, QuotaCheckKinds.ListingsCreate)
            .Tag(QuotaTagKeys.UserId, req.OwnerId)
            .Tag(QuotaTagKeys.ScopeKey, "listing_categories")
            .Tag(QuotaTagKeys.ScopeValue, category.Slug)
            .Execute(async ctx =>
            {
                // المعترض ربط العرض باشتراك - نستخدم بياناته من الـ context
                ctx.TryGet<Ashare.Api2.Entities.Subscription>("linked_subscription", out var sub);
                ctx.TryGet<Ashare.Api2.Entities.Plan>("linked_plan", out var plan);

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

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        listing.Status = ListingStatus.Published;
        listing.PublishedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(listing, ct);
        return this.OkEnvelope("listing.publish", listing);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _repo.SoftDeleteAsync(id, ct);
        return this.NoContentEnvelope("listing.delete");
    }
}
