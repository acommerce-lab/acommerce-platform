using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Listings.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Listings.Backend;

/// <summary>
/// نقاط نهاية الإعلانات. مسارات:
///   <c>GET  /listings</c>            — بحث/فلترة عامّ (لا يحتاج توثيق).
///   <c>GET  /listings/{id}</c>       — تفاصيل إعلان.
///   <c>GET  /home/explore</c>        — alias لـ /listings (واجهة إيجار + غيرها).
///   <c>GET  /my-listings</c>         — قائمة إعلانات المستخدم الحاليّ.
///   <c>POST /my-listings</c>         — إنشاء إعلان (OAM op listing.create).
///   <c>PATCH /my-listings/{id}</c>   — تعديل (OAM op listing.edit).
///   <c>POST /my-listings/{id}/toggle</c> — بدّل بين active/paused (listing.toggle).
///   <c>DELETE /my-listings/{id}</c>  — soft delete (listing.delete).
///
/// كلّ الـ writes تَستعمل نمط H3: POCO على ctx.WithEntity + AddNoSaveAsync
/// + SaveAtEnd. كلّ الـ post-interceptors (notify-watchers، …) ترى الكيان
/// عبر <c>ctx.Entity&lt;IListing&gt;()</c>.
/// </summary>
[ApiController]
public sealed class ListingsController : ControllerBase
{
    private readonly IListingStore _store;
    private readonly OpEngine _engine;
    private readonly ListingsKitOptions _options;

    public ListingsController(IListingStore store, OpEngine engine, ListingsKitOptions options)
    {
        _store = store; _engine = engine; _options = options;
    }

    private string? CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private string CallerPartyId(string id) => $"{_options.PartyKind}:{id}";

    // ─── GET /listings + GET /home/explore (alias) ────────────────────────
    [HttpGet("/listings")]
    [HttpGet("/home/explore")]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] string? city,
        [FromQuery] string? district,
        [FromQuery] string? propertyType,
        [FromQuery] string? category,        // alias لـ propertyType (واجهة Discovery)
        [FromQuery] string? timeUnit,
        [FromQuery(Name = "priceMin")] decimal? priceMin,
        [FromQuery(Name = "priceMax")] decimal? priceMax,
        [FromQuery(Name = "minPrice")] decimal? minPriceAlias,
        [FromQuery(Name = "maxPrice")] decimal? maxPriceAlias,
        [FromQuery] string? q,
        [FromQuery] int minBedrooms = 0,
        [FromQuery] int minAreaSqm = 0,
        [FromQuery] bool onlyVerified = false,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var pageSizeClamped = Math.Min(Math.Max(1, pageSize), _options.MaxPageSize);
        var filter = new ListingFilter(
            City: city, District: district,
            PropertyType: propertyType ?? category,
            TimeUnit: timeUnit,
            PriceMin: priceMin ?? minPriceAlias,
            PriceMax: priceMax ?? maxPriceAlias,
            Search: q,
            MinBedrooms: minBedrooms,
            MinAreaSqm: minAreaSqm,
            OnlyVerified: onlyVerified,
            Sort: sort,
            Page: Math.Max(1, page),
            PageSize: pageSizeClamped);

        var items = await _store.SearchAsync(filter, ct);
        var total = await _store.CountAsync(filter, ct);
        return this.OkEnvelope("listing.list", new
        {
            total, page = filter.Page, pageSize = filter.PageSize,
            items
        });
    }

    // ─── GET /listings/{id} ───────────────────────────────────────────────
    [HttpGet("/listings/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var l = await _store.GetAsync(id, ct);
        if (l is null) return this.NotFoundEnvelope("listing_not_found");

        // عداد المشاهدة — fire-and-forget داخل الطلب (لا OAM لأنّه نشاط
        // قراءة عابر، ليس حدثاً محاسبيّاً يستحقّ envelope كاملاً).
        try { await _store.IncrementViewCountNoSaveAsync(id, ct); } catch { }

        return this.OkEnvelope("listing.details", l);
    }

    // ─── GET /my-listings ────────────────────────────────────────────────
    [HttpGet("/my-listings")]
    [Authorize]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        var rows = await _store.ListByOwnerAsync(CallerId, ct);
        return this.OkEnvelope("listing.my", rows);
    }

    // ─── POST /my-listings ───────────────────────────────────────────────
    public sealed record CreateBody(
        string? Title, string? Description, decimal? Price,
        string? TimeUnit, string? PropertyType,
        string? City, string? District,
        double? Lat, double? Lng,
        int? BedroomCount, int? BathroomCount, int? AreaSqm,
        IReadOnlyList<string>? Amenities,
        IReadOnlyList<string>? Images,
        string? Thumbnail);

    [HttpPost("/my-listings")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateBody req, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();

        var listing = new InMemoryListing(
            Id:            Guid.NewGuid().ToString(),
            OwnerId:       CallerId,
            Title:         req.Title ?? "",
            Description:   req.Description ?? "",
            Price:         req.Price ?? 0m,
            TimeUnit:      req.TimeUnit ?? "monthly",
            PropertyType:  req.PropertyType ?? "apartment",
            City:          req.City ?? "",
            District:      req.District ?? "",
            Lat:           req.Lat ?? 0,
            Lng:           req.Lng ?? 0,
            BedroomCount:  req.BedroomCount ?? 0,
            BathroomCount: req.BathroomCount ?? 0,
            AreaSqm:       req.AreaSqm ?? 0,
            Status:        1,
            ViewsCount:    0,
            IsVerified:    false,
            ThumbnailUrl:  string.IsNullOrEmpty(req.Thumbnail) ? null : req.Thumbnail,
            Images:        req.Images ?? Array.Empty<string>(),
            Amenities:     req.Amenities ?? Array.Empty<string>(),
            CreatedAt:     DateTime.UtcNow);

        var op = Entry.Create(ListingOps.Create)
            .Describe($"User {CallerId} creates listing {listing.Id}")
            .From(CallerPartyId(CallerId), 1, ("role", "owner"))
            .To($"Listing:{listing.Id}", 1, ("role", "created"))
            .Mark(ListingMarkers.IsListing)
            .Tag(ListingTagKeys.OwnerId,      CallerId)
            .Tag(ListingTagKeys.PropertyType, listing.PropertyType)
            .Tag(ListingTagKeys.City,         listing.City)
            .Tag(ListingTagKeys.District,     listing.District)
            .Analyze(new RequiredFieldAnalyzer("title", () => req.Title))
            .Analyze(new MaxLengthAnalyzer  ("title", () => req.Title, _options.MaxTitleLength))
            .Analyze(new MaxLengthAnalyzer  ("description", () => req.Description, _options.MaxDescriptionLength))
            .Analyze(new RequiredFieldAnalyzer("city",  () => req.City))
            .Execute(async ctx =>
            {
                ctx.WithEntity<IListing>(listing);
                await _store.AddNoSaveAsync(listing, ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (object)listing, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "create_failed", env.Operation.ErrorMessage);

        return this.OkEnvelope(ListingOps.Create, listing);
    }

    // ─── PATCH /my-listings/{id} ─────────────────────────────────────────
    public sealed record EditBody(
        string? Title, string? Description, decimal? Price,
        string? TimeUnit, string? PropertyType,
        string? City, string? District,
        double? Lat, double? Lng,
        int? BedroomCount, int? BathroomCount, int? AreaSqm,
        IReadOnlyList<string>? Amenities,
        IReadOnlyList<string>? Images,
        string? Thumbnail);

    [HttpPatch("/my-listings/{id}")]
    [Authorize]
    public async Task<IActionResult> Edit(string id, [FromBody] EditBody req, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (!await _store.IsOwnerAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_owner");

        var patch = new ListingUpdate(
            req.Title, req.Description, req.Price, req.TimeUnit, req.PropertyType,
            req.City, req.District, req.Lat, req.Lng,
            req.BedroomCount, req.BathroomCount, req.AreaSqm,
            req.Amenities, req.Images, req.Thumbnail);

        var ok = false;
        var op = Entry.Create(ListingOps.Edit)
            .Describe($"User {CallerId} edits listing {id}")
            .From(CallerPartyId(CallerId), 1, ("role", "owner"))
            .To($"Listing:{id}", 1, ("role", "edited"))
            .Mark(ListingMarkers.IsListing)
            .Tag(ListingTagKeys.OwnerId, CallerId)
            .Execute(async ctx =>
            {
                ok = await _store.UpdateNoSaveAsync(id, patch, ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "edit_failed", env.Operation.ErrorMessage);
        if (!ok) return this.NotFoundEnvelope("listing_not_found");

        var fresh = await _store.GetAsync(id, ct);
        return this.OkEnvelope(ListingOps.Edit, (object?)fresh ?? new { id });
    }

    // ─── POST /my-listings/{id}/toggle ───────────────────────────────────
    [HttpPost("/my-listings/{id}/toggle")]
    [Authorize]
    public async Task<IActionResult> Toggle(string id, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (!await _store.IsOwnerAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_owner");

        int? newStatus = null;
        var op = Entry.Create(ListingOps.Toggle)
            .Describe($"User {CallerId} toggles listing {id}")
            .From(CallerPartyId(CallerId), 1, ("role", "owner"))
            .To($"Listing:{id}", 1, ("role", "toggled"))
            .Mark(ListingMarkers.IsListing)
            .Execute(async ctx =>
            {
                newStatus = await _store.ToggleStatusNoSaveAsync(id, ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "toggle_failed", env.Operation.ErrorMessage);
        if (newStatus is null) return this.NotFoundEnvelope("listing_not_found");

        return this.OkEnvelope(ListingOps.Toggle, new { id, status = newStatus });
    }

    // ─── DELETE /my-listings/{id} ────────────────────────────────────────
    [HttpDelete("/my-listings/{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (!await _store.IsOwnerAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_owner");

        var ok = false;
        var op = Entry.Create(ListingOps.Delete)
            .Describe($"User {CallerId} deletes listing {id}")
            .From(CallerPartyId(CallerId), 1, ("role", "owner"))
            .To($"Listing:{id}", 1, ("role", "deleted"))
            .Mark(ListingMarkers.IsListing)
            .Execute(async ctx =>
            {
                ok = await _store.DeleteNoSaveAsync(id, ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "delete_failed", env.Operation.ErrorMessage);
        if (!ok) return this.NotFoundEnvelope("listing_not_found");

        return this.OkEnvelope(ListingOps.Delete, new { id, deleted = true });
    }
}
