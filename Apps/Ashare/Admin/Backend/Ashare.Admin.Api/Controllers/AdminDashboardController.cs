using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public class AdminDashboardController : ControllerBase
{
    private readonly IBaseAsyncRepository<User> _users;
    private readonly IBaseAsyncRepository<Listing> _listings;
    private readonly IBaseAsyncRepository<Booking> _bookings;
    private readonly IBaseAsyncRepository<Subscription> _subscriptions;
    private readonly IBaseAsyncRepository<Payment> _payments;

    public AdminDashboardController(IRepositoryFactory factory)
    {
        _users         = factory.CreateRepository<User>();
        _listings      = factory.CreateRepository<Listing>();
        _bookings      = factory.CreateRepository<Booking>();
        _subscriptions = factory.CreateRepository<Subscription>();
        _payments      = factory.CreateRepository<Payment>();
    }

    /// <summary>
    /// GET /api/admin/dashboard
    /// إحصائيات موجزة للوحة الإدارة.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        // Users
        var totalUsers   = await _users.CountAsync(cancellationToken: ct);
        var activeUsers  = await _users.CountAsync(u => u.IsActive, ct);
        var suspendedUsers = totalUsers - activeUsers;

        // Listings by status
        var allListings          = await _listings.GetAllWithPredicateAsync(l => true);
        var totalListings        = allListings.Count();
        var draftListings        = allListings.Count(l => l.Status == ListingStatus.Draft);
        var publishedListings    = allListings.Count(l => l.Status == ListingStatus.Published);
        var reservedListings     = allListings.Count(l => l.Status == ListingStatus.Reserved);
        var rejectedListings     = allListings.Count(l => l.Status == ListingStatus.Rejected);
        var closedListings       = allListings.Count(l => l.Status == ListingStatus.Closed);
        var featuredListings     = allListings.Count(l => l.IsFeatured);

        // Bookings by status
        var allBookings          = await _bookings.GetAllWithPredicateAsync(b => true);
        var totalBookings        = allBookings.Count();
        var pendingBookings      = allBookings.Count(b => b.Status == BookingStatus.Pending);
        var confirmedBookings    = allBookings.Count(b => b.Status == BookingStatus.Confirmed);
        var paidBookings         = allBookings.Count(b => b.Status == BookingStatus.Paid);
        var cancelledBookings    = allBookings.Count(b => b.Status == BookingStatus.Cancelled);
        var completedBookings    = allBookings.Count(b => b.Status == BookingStatus.Completed);

        // Subscriptions
        var allSubs              = await _subscriptions.GetAllWithPredicateAsync(s => true);
        var totalSubscriptions   = allSubs.Count();
        var activeSubscriptions  = allSubs.Count(s => s.IsCurrentlyActive);
        var expiredSubscriptions = allSubs.Count(s => s.Status == SubscriptionStatus.Expired);
        var cancelledSubs        = allSubs.Count(s => s.Status == SubscriptionStatus.Cancelled);

        // Revenue: مجموع المدفوعات الناجحة
        var allPayments          = await _payments.GetAllWithPredicateAsync(p => true);
        var totalRevenue         = allPayments
            .Where(p => p.Status == PaymentStatus.Captured)
            .Sum(p => p.Amount);
        var pendingRevenue       = allPayments
            .Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Authorized)
            .Sum(p => p.Amount);

        var stats = new
        {
            users = new
            {
                total     = totalUsers,
                active    = activeUsers,
                suspended = suspendedUsers
            },
            listings = new
            {
                total     = totalListings,
                draft     = draftListings,
                published = publishedListings,
                reserved  = reservedListings,
                rejected  = rejectedListings,
                closed    = closedListings,
                featured  = featuredListings
            },
            bookings = new
            {
                total     = totalBookings,
                pending   = pendingBookings,
                confirmed = confirmedBookings,
                paid      = paidBookings,
                cancelled = cancelledBookings,
                completed = completedBookings
            },
            subscriptions = new
            {
                total     = totalSubscriptions,
                active    = activeSubscriptions,
                expired   = expiredSubscriptions,
                cancelled = cancelledSubs
            },
            revenue = new
            {
                totalCaptured = totalRevenue,
                pending       = pendingRevenue,
                currency      = "SAR"
            }
        };

        return this.OkEnvelope("admin.dashboard.read", stats);
    }
}
