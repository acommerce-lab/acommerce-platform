using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Order.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Order.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public class AdminDashboardController : ControllerBase
{
    private readonly IBaseAsyncRepository<User> _users;
    private readonly IBaseAsyncRepository<Vendor> _vendors;
    private readonly IBaseAsyncRepository<OrderRecord> _orders;
    private readonly IBaseAsyncRepository<Offer> _offers;

    public AdminDashboardController(IRepositoryFactory factory)
    {
        _users   = factory.CreateRepository<User>();
        _vendors = factory.CreateRepository<Vendor>();
        _orders  = factory.CreateRepository<OrderRecord>();
        _offers  = factory.CreateRepository<Offer>();
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
        var activeUsers  = await _users.CountAsync(u => u.IsActive, cancellationToken: ct);
        var suspendedUsers = totalUsers - activeUsers;

        // Vendors
        var allVendors       = await _vendors.GetAllWithPredicateAsync(v => true);
        var totalVendors     = allVendors.Count();
        var activeVendors    = allVendors.Count(v => v.IsActive);
        var suspendedVendors = totalVendors - activeVendors;

        // Orders by status
        var allOrders         = await _orders.GetAllWithPredicateAsync(o => true);
        var totalOrders       = allOrders.Count();
        var pendingOrders     = allOrders.Count(o => o.Status == OrderStatus.Pending);
        var acceptedOrders    = allOrders.Count(o => o.Status == OrderStatus.Accepted);
        var readyOrders       = allOrders.Count(o => o.Status == OrderStatus.Ready);
        var deliveredOrders   = allOrders.Count(o => o.Status == OrderStatus.Delivered);
        var cancelledOrders   = allOrders.Count(o => o.Status == OrderStatus.Cancelled);

        // Offers
        var allOffers         = await _offers.GetAllWithPredicateAsync(o => true);
        var totalOffers       = allOffers.Count();
        var activeOffers      = allOffers.Count(o => o.IsActive);
        var featuredOffers    = allOffers.Count(o => o.IsFeatured);

        // Revenue: مجموع الطلبات المكتملة (Delivered)
        var totalRevenue = allOrders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Sum(o => o.Total);
        var pendingRevenue = allOrders
            .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Ready)
            .Sum(o => o.Total);

        var stats = new
        {
            // Flat shape consumed by Dashboard.razor — every property the
            // frontend DashboardApiRow declares must appear at this level.
            totalUsers,
            totalVendors,
            totalOrders,
            pendingOrders,
            activeOffers,
            totalRevenue,

            // Nested breakdowns kept for future admin widgets / BI.
            users = new
            {
                total     = totalUsers,
                active    = activeUsers,
                suspended = suspendedUsers
            },
            vendors = new
            {
                total     = totalVendors,
                active    = activeVendors,
                suspended = suspendedVendors
            },
            orders = new
            {
                total     = totalOrders,
                pending   = pendingOrders,
                accepted  = acceptedOrders,
                ready     = readyOrders,
                delivered = deliveredOrders,
                cancelled = cancelledOrders
            },
            offers = new
            {
                total    = totalOffers,
                active   = activeOffers,
                featured = featuredOffers
            },
            revenue = new
            {
                totalDelivered = totalRevenue,
                pending        = pendingRevenue,
                currency       = "SAR"
            }
        };

        return this.OkEnvelope("admin.dashboard.read", stats);
    }
}
