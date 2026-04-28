using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Domain;

namespace Order.V2.Vendor.Api.Controllers;

[ApiController]
[Route("api/vendor/orders")]
[Authorize(Policy = "VendorOnly")]
public class VendorOrdersController : ControllerBase
{
    private readonly IBaseAsyncRepository<OrderRecord> _orders;
    private readonly IBaseAsyncRepository<OrderItem>   _items;
    private readonly OpEngine _engine;

    public VendorOrdersController(IRepositoryFactory repo, OpEngine engine)
    {
        _orders = repo.CreateRepository<OrderRecord>();
        _items  = repo.CreateRepository<OrderItem>();
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);

        var all = (await _orders.ListAllAsync(ct))
            .Where(o => o.VendorId == vendorId && !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.CustomerId,
                o.Total, o.Currency, Status = o.Status.ToString(),
                o.PickupType, o.CarModel, o.CarColor, o.CarPlate,
                o.CustomerNotes, o.CreatedAt
            }).ToList();

        return this.OkEnvelope("vendor.orders.list", all);
    }

    [HttpPost("{id}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null || order.VendorId != vendorId) return this.NotFoundEnvelope("order_not_found");
        if (order.Status != OrderStatus.Pending) return this.BadRequestEnvelope("invalid_status", "الطلب ليس في حالة انتظار.");

        var op = Entry.Create("vendor.order.accept")
            .Describe($"Vendor:{vendorId} accepts Order:{id}")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"Order:{id}", 1, ("role", "order"))
            .Tag("order_id", id.ToString())
            .Execute(async ctx => { order.Status = OrderStatus.Accepted; await _orders.UpdateAsync(order, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("accept_failed", result.ErrorMessage);
        return this.OkEnvelope("vendor.order.accept", new { orderId = id, status = "Accepted" });
    }

    [HttpPost("{id}/ready")]
    public async Task<IActionResult> Ready(Guid id, CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null || order.VendorId != vendorId) return this.NotFoundEnvelope("order_not_found");
        if (order.Status != OrderStatus.Accepted) return this.BadRequestEnvelope("invalid_status", "الطلب ليس مقبولاً بعد.");

        var op = Entry.Create("vendor.order.ready")
            .Describe($"Vendor:{vendorId} marks Order:{id} as ready")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"Order:{id}", 1, ("role", "order"))
            .Execute(async ctx => { order.Status = OrderStatus.Ready; await _orders.UpdateAsync(order, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("ready_failed", result.ErrorMessage);
        return this.OkEnvelope("vendor.order.ready", new { orderId = id, status = "Ready" });
    }

    [HttpPost("{id}/deliver")]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null || order.VendorId != vendorId) return this.NotFoundEnvelope("order_not_found");
        if (order.Status != OrderStatus.Ready) return this.BadRequestEnvelope("invalid_status", "الطلب ليس جاهزاً بعد.");

        var op = Entry.Create("vendor.order.deliver")
            .Describe($"Vendor:{vendorId} delivers Order:{id}")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"Order:{id}", 1, ("role", "order"))
            .Execute(async ctx => { order.Status = OrderStatus.Delivered; await _orders.UpdateAsync(order, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("deliver_failed", result.ErrorMessage);
        return this.OkEnvelope("vendor.order.deliver", new { orderId = id, status = "Delivered" });
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null || order.VendorId != vendorId) return this.NotFoundEnvelope("order_not_found");
        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            return this.BadRequestEnvelope("invalid_status", "لا يمكن إلغاء هذا الطلب.");

        var op = Entry.Create("vendor.order.cancel")
            .Describe($"Vendor:{vendorId} cancels Order:{id}")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"Order:{id}", 1, ("role", "order"))
            .Execute(async ctx => { order.Status = OrderStatus.Cancelled; await _orders.UpdateAsync(order, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("cancel_failed", result.ErrorMessage);
        return this.OkEnvelope("vendor.order.cancel", new { orderId = id, status = "Cancelled" });
    }
}
