using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.V2.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public class AdminDashboardController : ControllerBase
{
    private readonly IBaseAsyncRepository<Profile>         _profiles;
    private readonly IBaseAsyncRepository<ProductListing>  _listings;
    private readonly IBaseAsyncRepository<Booking>         _bookings;
    private readonly IBaseAsyncRepository<Subscription>    _subscriptions;
    private readonly IBaseAsyncRepository<Payment>         _payments;

    public AdminDashboardController(IRepositoryFactory factory)
    {
        _profiles      = factory.CreateRepository<Profile>();
        _listings      = factory.CreateRepository<ProductListing>();
        _bookings      = factory.CreateRepository<Booking>();
        _subscriptions = factory.CreateRepository<Subscription>();
        _payments      = factory.CreateRepository<Payment>();
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var totalUsers    = await _profiles.CountAsync(cancellationToken: ct);
        var activeUsers   = await _profiles.CountAsync(p => p.IsActive, cancellationToken: ct);

        var allListings       = await _listings.GetAllWithPredicateAsync(l => true);
        var totalListings     = allListings.Count();
        var publishedListings = allListings.Count(l => l.Status == 1);
        var pendingListings   = allListings.Count(l => l.Status == 0);
        var featuredListings  = allListings.Count(l => l.IsFeatured);

        var allBookings      = await _bookings.GetAllWithPredicateAsync(b => true);
        var totalBookings    = allBookings.Count();
        var pendingBookings  = allBookings.Count(b => b.Status == "pending");
        var confirmedBookings = allBookings.Count(b => b.Status == "confirmed");
        var completedBookings = allBookings.Count(b => b.Status == "completed");

        var allSubs        = await _subscriptions.GetAllWithPredicateAsync(s => true);
        var activeSubs     = allSubs.Count(s => s.Status == "active" && s.PeriodEnd > DateTime.UtcNow);

        var allPayments    = await _payments.GetAllWithPredicateAsync(p => true);
        var totalRevenue   = allPayments.Where(p => p.Status == "captured").Sum(p => p.Amount);

        return this.OkEnvelope("admin.dashboard.read", new
        {
            totalUsers,
            totalListings,
            pendingListings,
            totalBookings,
            activeSubscriptions = activeSubs,
            totalRevenue,

            users = new { total = totalUsers, active = activeUsers, suspended = totalUsers - activeUsers },
            listings = new
            {
                total     = totalListings,
                pending   = pendingListings,
                published = publishedListings,
                featured  = featuredListings
            },
            bookings = new
            {
                total     = totalBookings,
                pending   = pendingBookings,
                confirmed = confirmedBookings,
                completed = completedBookings
            },
            subscriptions = new { total = allSubs.Count(), active = activeSubs },
            revenue = new { totalCaptured = totalRevenue, currency = "SAR" }
        });
    }
}
