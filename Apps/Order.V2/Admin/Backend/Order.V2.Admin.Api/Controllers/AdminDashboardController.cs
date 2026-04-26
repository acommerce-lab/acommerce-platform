using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Domain;

namespace Order.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public class AdminDashboardController : ControllerBase
{
    private readonly IBaseAsyncRepository<User>        _users;
    private readonly IBaseAsyncRepository<Vendor>      _vendors;
    private readonly IBaseAsyncRepository<OrderRecord> _orders;
    private readonly IBaseAsyncRepository<Offer>       _offers;

    public AdminDashboardController(IRepositoryFactory repo)
    {
        _users   = repo.CreateRepository<User>();
        _vendors = repo.CreateRepository<Vendor>();
        _orders  = repo.CreateRepository<OrderRecord>();
        _offers  = repo.CreateRepository<Offer>();
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var allUsers   = await _users.GetAllWithPredicateAsync(u => !u.IsDeleted);
        var allVendors = await _vendors.GetAllWithPredicateAsync(v => !v.IsDeleted);
        var allOrders  = await _orders.GetAllWithPredicateAsync(o => !o.IsDeleted);
        var allOffers  = await _offers.GetAllWithPredicateAsync(o => !o.IsDeleted);

        var totalRevenue = allOrders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Sum(o => o.Total);

        return this.OkEnvelope("admin.dashboard.read", new
        {
            totalUsers   = allUsers.Count(),
            activeUsers  = allUsers.Count(u => u.IsActive),
            totalVendors = allVendors.Count(),
            totalOrders  = allOrders.Count(),
            pendingOrders = allOrders.Count(o => o.Status == OrderStatus.Pending),
            totalOffers  = allOffers.Count(),
            activeOffers = allOffers.Count(o => o.IsActive),
            totalRevenue,
        });
    }
}
