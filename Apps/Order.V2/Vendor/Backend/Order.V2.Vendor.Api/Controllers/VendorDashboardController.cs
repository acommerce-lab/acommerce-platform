using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Api.Entities;

namespace Order.V2.Vendor.Api.Controllers;

[ApiController]
[Route("api/vendor/dashboard")]
[Authorize(Policy = "VendorOnly")]
public class VendorDashboardController : ControllerBase
{
    private readonly IBaseAsyncRepository<VendorEntity> _vendors;
    private readonly IBaseAsyncRepository<OrderRecord> _orders;
    private readonly IBaseAsyncRepository<Offer>       _offers;

    public VendorDashboardController(IRepositoryFactory repo)
    {
        _vendors = repo.CreateRepository<VendorEntity>();
        _orders  = repo.CreateRepository<OrderRecord>();
        _offers  = repo.CreateRepository<Offer>();
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);

        var vendor = await _vendors.GetByIdAsync(vendorId, ct);
        if (vendor is null) return this.NotFoundEnvelope("vendor_not_found");

        var orders = await _orders.GetAllWithPredicateAsync(o => o.VendorId == vendorId && !o.IsDeleted);
        var offers = await _offers.GetAllWithPredicateAsync(o => o.VendorId == vendorId && !o.IsDeleted);

        var totalRevenue = orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total);

        return this.OkEnvelope("vendor.dashboard.read", new
        {
            vendorId        = vendor.Id,
            vendorName      = vendor.Name,
            logoEmoji       = vendor.LogoEmoji,
            rating          = vendor.Rating,
            ratingCount     = vendor.RatingCount,
            totalOrders     = orders.Count(),
            pendingOrders   = orders.Count(o => o.Status == OrderStatus.Pending),
            activeOrders    = orders.Count(o => o.Status is OrderStatus.Accepted or OrderStatus.Ready),
            completedOrders = orders.Count(o => o.Status == OrderStatus.Delivered),
            totalOffers     = offers.Count(),
            activeOffers    = offers.Count(o => o.IsActive),
            totalRevenue,
        });
    }
}
