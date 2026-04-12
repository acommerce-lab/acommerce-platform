using ACommerce.Notification.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Vendor.Api.Entities;
using Vendor.Api.Services;

namespace Vendor.Api.Controllers;

/// <summary>
/// Vendor-side order lifecycle. Receives webhooks from Order.Api,
/// lets the vendor accept/reject/ready/deliver, and sends callbacks.
///
/// Every mutation is an OpEngine accounting entry. Interceptors
/// (WorkScheduleGate, AcceptanceGate) run automatically on operations
/// tagged with "vendor_order".
/// </summary>
[ApiController]
[Route("api/vendor-orders")]
public class VendorOrdersController : ControllerBase
{
    private readonly IBaseAsyncRepository<IncomingOrder> _orders;
    private readonly IBaseAsyncRepository<VendorSettings> _settings;
    private readonly OpEngine _engine;
    private readonly OrderApiCallback _callback;
    private readonly Notifier _notifier;

    public VendorOrdersController(
        IRepositoryFactory factory, OpEngine engine, OrderApiCallback callback, Notifier notifier)
    {
        _orders = factory.CreateRepository<IncomingOrder>();
        _settings = factory.CreateRepository<VendorSettings>();
        _engine = engine;
        _callback = callback;
        _notifier = notifier;
    }

    // ── Webhook: Order.Api sends us a new order ──────────────────────────
    public record IncomingOrderWebhook(
        Guid OrderApiId,
        Guid VendorId,
        string OrderNumber,
        string CustomerName,
        string? CustomerPhone,
        decimal Total,
        string Currency,
        string? ItemsSummary,
        int PickupType,
        string? CarModel,
        string? CarColor,
        string? CarPlate,
        string? CustomerNotes);

    [HttpPost("incoming")]
    public async Task<IActionResult> ReceiveIncoming([FromBody] IncomingOrderWebhook req, CancellationToken ct)
    {
        // Check if already received (idempotency)
        var existing = await _orders.GetAllWithPredicateAsync(
            o => o.OrderApiId == req.OrderApiId);
        if (existing.Count > 0)
            return this.OkEnvelope("order.receive", new { id = existing.First().Id, status = "already_received" });

        // Resolve timeout from vendor settings
        var settings = (await _settings.GetAllWithPredicateAsync(
            s => s.VendorId == req.VendorId)).FirstOrDefault();
        var timeoutMinutes = settings?.OrderTimeoutMinutes ?? 10;

        var order = new IncomingOrder
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OrderApiId = req.OrderApiId,
            VendorId = req.VendorId,
            OrderNumber = req.OrderNumber,
            CustomerName = req.CustomerName,
            CustomerPhone = req.CustomerPhone,
            Total = req.Total,
            Currency = req.Currency,
            ItemsSummary = req.ItemsSummary,
            PickupType = req.PickupType,
            CarModel = req.CarModel,
            CarColor = req.CarColor,
            CarPlate = req.CarPlate,
            CustomerNotes = req.CustomerNotes,
            Status = IncomingOrderStatus.Pending,
            ReceivedAt = DateTime.UtcNow,
            TimeoutAt = DateTime.UtcNow.AddMinutes(timeoutMinutes),
        };

        // OpEngine entry — tagged "vendor_order" so interceptors (schedule/acceptance gates) fire
        var op = Entry.Create("order.receive")
            .Describe($"Incoming order {req.OrderNumber} for Vendor:{req.VendorId}")
            .From($"OrderApi:{req.OrderApiId}", req.Total, ("role", "platform"), ("currency", req.Currency))
            .To($"Vendor:{req.VendorId}", req.Total, ("role", "vendor"), ("currency", req.Currency))
            .Tag("vendor_order", "receive")
            .Tag("vendor_id", req.VendorId.ToString())
            .Tag("order_number", req.OrderNumber)
            .Execute(async ctx =>
            {
                await _orders.AddAsync(order, ctx.CancellationToken);
                ctx.Set("incomingOrderId", order.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, order, ct);
        if (envelope.Operation.Status != "Success")
        {
            // Interceptor blocked (closed, not accepting, etc.) → auto-reject callback
            await _callback.NotifyStatusAsync(req.OrderApiId, "rejected", ct);
            return this.OkEnvelope("order.receive.rejected", new
            {
                reason = envelope.Operation.FailedAnalyzer ?? envelope.Operation.ErrorMessage
            });
        }

        // Notify the vendor user via Notifier (OpEngine operation)
        await _notifier.SendAsync(
            VendorNotifications.OrderReceived,
            PartyId.User(req.VendorId.ToString()),
            new { order.Id, order.OrderNumber, order.Total, order.CustomerName },
            titleOverride: $"طلب جديد #{req.OrderNumber}",
            messageOverride: $"{req.CustomerName} • {req.Total:0.##} {req.Currency}", ct: ct);

        return this.OkEnvelope("order.receive", new
        {
            id = order.Id,
            status = "pending",
            timeoutAt = order.TimeoutAt,
        });
    }

    // ── Vendor actions ───────────────────────────────────────────────────

    [HttpGet("by-vendor/{vendorId:guid}")]
    public async Task<IActionResult> List(Guid vendorId, CancellationToken ct)
    {
        var all = await _orders.GetAllWithPredicateAsync(o => o.VendorId == vendorId);
        var sorted = all.OrderByDescending(o => o.ReceivedAt).ToList();
        return this.OkEnvelope("vendor-order.list", sorted.Select(o => new
        {
            o.Id, o.OrderApiId, o.OrderNumber, o.CustomerName, o.CustomerPhone,
            o.Total, o.Currency, o.ItemsSummary, o.PickupType,
            o.CarModel, o.CarColor, o.CarPlate, o.CustomerNotes,
            o.Status, o.ReceivedAt, o.TimeoutAt, o.RespondedAt,
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        return o == null ? this.NotFoundEnvelope("order_not_found") : this.OkEnvelope("vendor-order.get", o);
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        if (o.Status != IncomingOrderStatus.Pending)
            return this.BadRequestEnvelope("not_pending", "الطلب ليس في حالة انتظار");

        var op = Entry.Create("order.accept")
            .Describe($"Vendor:{o.VendorId} accepts order {o.OrderNumber}")
            .From($"Vendor:{o.VendorId}", 1, ("role", "acceptor"))
            .To($"Order:{o.OrderApiId}", 1, ("role", "order"))
            .Tag("order_number", o.OrderNumber)
            .Tag("vendor_order", "accept")
            .Execute(async ctx =>
            {
                o.Status = IncomingOrderStatus.Accepted;
                o.RespondedAt = DateTime.UtcNow;
                o.UpdatedAt = DateTime.UtcNow;
                await _orders.UpdateAsync(o, ctx.CancellationToken);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { o.Id, status = "Accepted" }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);

        o.ResponseOperationId = envelope.Operation.Id;
        await _orders.UpdateAsync(o, ct);

        await _callback.NotifyStatusAsync(o.OrderApiId, "accepted", ct);
        return this.OkEnvelope("order.accept", new { o.Id, status = o.Status.ToString() });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        if (o.Status != IncomingOrderStatus.Pending)
            return this.BadRequestEnvelope("not_pending", "الطلب ليس في حالة انتظار");

        var op = Entry.Create("order.reject")
            .Describe($"Vendor:{o.VendorId} rejects order {o.OrderNumber}")
            .From($"Vendor:{o.VendorId}", o.Total, ("role", "vendor"), ("currency", o.Currency))
            .To($"Order:{o.OrderApiId}", o.Total, ("role", "order"))
            .Tag("order_number", o.OrderNumber)
            .Tag("vendor_order", "reject")
            .Execute(async ctx =>
            {
                o.Status = IncomingOrderStatus.Rejected;
                o.RespondedAt = DateTime.UtcNow;
                o.UpdatedAt = DateTime.UtcNow;
                await _orders.UpdateAsync(o, ctx.CancellationToken);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { o.Id, status = "Rejected" }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);

        await _callback.NotifyStatusAsync(o.OrderApiId, "rejected", ct);
        return this.OkEnvelope("order.reject", new { o.Id, status = o.Status.ToString() });
    }

    [HttpPost("{id:guid}/ready")]
    public async Task<IActionResult> Ready(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        if (o.Status != IncomingOrderStatus.Accepted)
            return this.BadRequestEnvelope("not_accepted", "يجب قبول الطلب أولاً");

        var op = Entry.Create("order.ready")
            .Describe($"Order {o.OrderNumber} ready for pickup")
            .From($"Vendor:{o.VendorId}", 1, ("role", "preparer"))
            .To($"Order:{o.OrderApiId}", 1, ("role", "order"))
            .Tag("order_number", o.OrderNumber)
            .Tag("vendor_order", "ready")
            .Execute(async ctx =>
            {
                o.Status = IncomingOrderStatus.Ready;
                o.UpdatedAt = DateTime.UtcNow;
                await _orders.UpdateAsync(o, ctx.CancellationToken);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { o.Id, status = "Ready" }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);

        await _callback.NotifyStatusAsync(o.OrderApiId, "ready", ct);
        return this.OkEnvelope("order.ready", new { o.Id, status = o.Status.ToString() });
    }

    [HttpPost("{id:guid}/deliver")]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        if (o.Status != IncomingOrderStatus.Ready)
            return this.BadRequestEnvelope("not_ready", "الطلب ليس جاهزاً");

        var op = Entry.Create("order.deliver")
            .Describe($"Order {o.OrderNumber} delivered — value transferred")
            .From($"Order:{o.OrderApiId}", o.Total, ("role", "order"), ("currency", o.Currency))
            .To($"Customer:via-{o.OrderApiId}", o.Total, ("role", "customer"), ("currency", o.Currency))
            .Tag("order_number", o.OrderNumber)
            .Tag("vendor_order", "deliver")
            .Execute(async ctx =>
            {
                o.Status = IncomingOrderStatus.Delivered;
                o.UpdatedAt = DateTime.UtcNow;
                await _orders.UpdateAsync(o, ctx.CancellationToken);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { o.Id, status = "Delivered" }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);

        await _callback.NotifyStatusAsync(o.OrderApiId, "delivered", ct);
        return this.OkEnvelope("order.deliver", new { o.Id, status = o.Status.ToString() });
    }
}
