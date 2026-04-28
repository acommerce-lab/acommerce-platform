using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Domain;

namespace Order.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = "AdminOnly")]
public class AdminOrdersController : ControllerBase
{
    private readonly IBaseAsyncRepository<OrderRecord> _orders;

    public AdminOrdersController(IRepositoryFactory repo)
        => _orders = repo.CreateRepository<OrderRecord>();

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = (await _orders.ListAllAsync(ct))
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.CustomerId, o.VendorId,
                o.Total, o.Currency, Status = o.Status.ToString(),
                o.PickupType, o.CreatedAt
            }).ToList();
        return this.OkEnvelope("admin.orders.list", all);
    }
}
