using ACommerce.SharedKernel.Abstractions.Repositories;
using Order.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Order.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = "AdminOnly")]
public class AdminOrdersController : ControllerBase
{
    private readonly IBaseAsyncRepository<OrderRecord> _repo;

    public AdminOrdersController(IRepositoryFactory factory)
    {
        _repo = factory.CreateRepository<OrderRecord>();
    }

    /// <summary>
    /// GET /api/admin/orders?status=&amp;page=1&amp;pageSize=20
    /// قائمة الطلبات مع فلترة وترقيم.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        OrderStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var s))
            parsedStatus = s;

        // Frontend expects a flat list — return `.Items` directly.
        var result = await _repo.GetPagedAsync(
            pageNumber: page,
            pageSize: pageSize,
            predicate: o => parsedStatus == null || o.Status == parsedStatus,
            orderBy: o => o.CreatedAt,
            ascending: false);

        var rows = result.Items.Select(o => new
        {
            id            = o.Id,
            customerName  = (string?)null,
            customerPhone = (string?)null,
            totalPrice    = o.Total,
            currency      = o.Currency,
            status        = (int)o.Status,
            createdAt     = o.CreatedAt,
            notes         = (string?)null
        }).ToList();

        return this.OkEnvelope("admin.order.list", rows);
    }

    /// <summary>
    /// GET /api/admin/orders/{id}
    /// تفاصيل طلب.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(id, ct);
        if (order == null) return this.NotFoundEnvelope("order_not_found");
        return this.OkEnvelope("admin.order.get", order);
    }
}
