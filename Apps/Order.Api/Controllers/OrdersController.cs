using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api.Entities;
using Order.Api.Services;

namespace Order.Api.Controllers;

/// <summary>
/// إدارة طلبات اوردر. الإنشاء يمر عبر OperationEngine كقيد محاسبي:
/// العميل (مدين) ← التاجر (دائن) ببنود الطلب. بعد الإنشاء يُرسل webhook
/// إلى Vendor.Api، الذي يردّ عبر /vendor-callback بالقبول أو الرفض أو المهلة.
/// </summary>
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IBaseAsyncRepository<OrderRecord> _orders;
    private readonly IBaseAsyncRepository<OrderItem> _items;
    private readonly IBaseAsyncRepository<Offer> _offers;
    private readonly IBaseAsyncRepository<Vendor> _vendors;
    private readonly IBaseAsyncRepository<User> _users;
    private readonly OpEngine _engine;
    private readonly VendorApiNotifier _vendorNotifier;

    public OrdersController(IRepositoryFactory factory, OpEngine engine, VendorApiNotifier vendorNotifier)
    {
        _vendorNotifier = vendorNotifier;
        _orders = factory.CreateRepository<OrderRecord>();
        _items = factory.CreateRepository<OrderItem>();
        _offers = factory.CreateRepository<Offer>();
        _vendors = factory.CreateRepository<Vendor>();
        _users = factory.CreateRepository<User>();
        _engine = engine;
    }

    public record OrderItemRequest(Guid OfferId, int Quantity);

    public record CreateOrderRequest(
        Guid CustomerId,
        List<OrderItemRequest> Items,
        PickupType PickupType,
        PaymentMethod PreferredPayment,
        decimal? CashTendered,
        string? CarModel,
        string? CarColor,
        string? CarPlate,
        double? PickupLatitude,
        double? PickupLongitude,
        string? CustomerNotes);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req, CancellationToken ct)
    {
        if (req.Items == null || req.Items.Count == 0)
            return this.BadRequestEnvelope("empty_cart", "السلة فارغة");

        // Resolve offers and ensure same vendor
        var offers = new List<(Offer Offer, int Quantity)>();
        Guid? vendorId = null;
        foreach (var it in req.Items)
        {
            var o = await _offers.GetByIdAsync(it.OfferId, ct);
            if (o == null) return this.BadRequestEnvelope("offer_not_found", $"العرض {it.OfferId} غير موجود");
            if (vendorId == null) vendorId = o.VendorId;
            else if (vendorId != o.VendorId)
                return this.BadRequestEnvelope("multi_vendor_cart", "كل العناصر يجب أن تكون من متجر واحد");
            offers.Add((o, Math.Max(1, it.Quantity)));
        }
        var vendor = await _vendors.GetByIdAsync(vendorId!.Value, ct);
        if (vendor == null) return this.NotFoundEnvelope("vendor_not_found");

        // Compute totals
        var subtotal = offers.Sum(x => x.Offer.Price * x.Quantity);
        var orderId = Guid.NewGuid();
        var orderNumber = $"ORD-{DateTime.UtcNow:yyMMdd}-{orderId.ToString()[..6].ToUpper()}";

        var record = new OrderRecord
        {
            Id = orderId,
            CreatedAt = DateTime.UtcNow,
            OrderNumber = orderNumber,
            CustomerId = req.CustomerId,
            VendorId = vendor.Id,
            PickupType = req.PickupType,
            PreferredPayment = req.PreferredPayment,
            CashTendered = req.PreferredPayment == PaymentMethod.Cash ? req.CashTendered : null,
            CarModel = req.PickupType == PickupType.Curbside ? req.CarModel : null,
            CarColor = req.PickupType == PickupType.Curbside ? req.CarColor : null,
            CarPlate = req.PickupType == PickupType.Curbside ? req.CarPlate : null,
            PickupLatitude = req.PickupLatitude,
            PickupLongitude = req.PickupLongitude,
            Subtotal = subtotal,
            Total = subtotal,
            Currency = "SAR",
            CustomerNotes = req.CustomerNotes,
            Status = OrderStatus.Pending,
        };

        // Save default car details on the user for next time
        if (req.PickupType == PickupType.Curbside)
        {
            var user = await _users.GetByIdAsync(req.CustomerId, ct);
            if (user != null)
            {
                user.CarModel = req.CarModel ?? user.CarModel;
                user.CarColor = req.CarColor ?? user.CarColor;
                user.CarPlate = req.CarPlate ?? user.CarPlate;
                await _users.UpdateAsync(user, ct);
            }
        }

        // Accounting entry: customer (debit) <- vendor (credit) by order items
        var op = Entry.Create("order.create")
            .Describe($"Order {orderNumber} from User:{req.CustomerId} to Vendor:{vendor.Id}")
            .From($"User:{req.CustomerId}", subtotal, ("role", "customer"), ("currency", "SAR"))
            .To($"Vendor:{vendor.Id}", subtotal, ("role", "vendor"), ("currency", "SAR"))
            .Tag("pickup_type", req.PickupType.ToString())
            .Tag("payment_method", req.PreferredPayment.ToString())
            .Tag("order_number", orderNumber)
            .Execute(async ctx =>
            {
                await _orders.AddAsync(record, ctx.CancellationToken);
                foreach (var (offer, qty) in offers)
                {
                    await _items.AddAsync(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow,
                        OrderId = record.Id,
                        OfferId = offer.Id,
                        OfferTitle = offer.Title,
                        Emoji = offer.Emoji,
                        Quantity = qty,
                        UnitPrice = offer.Price,
                        LineTotal = offer.Price * qty
                    }, ctx.CancellationToken);
                }
                ctx.Set("orderId", record.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, record, ct);
        if (envelope.Operation.Status != "Success")
            return BadRequest(envelope);

        record.OperationId = envelope.Operation.Id;
        await _orders.UpdateAsync(record, ct);

        // ── Webhook: notify Vendor.Api about the new order ──────────────
        var itemsSummary = string.Join(", ", offers.Select(x => $"{x.Offer.Title} ×{x.Quantity}"));
        var customer = await _users.GetByIdAsync(req.CustomerId, ct);
        _ = _vendorNotifier.NotifyNewOrderAsync(
            record.Id, vendor.Id, record.OrderNumber,
            customer?.FullName ?? customer?.PhoneNumber ?? "",
            customer?.PhoneNumber,
            record.Total, record.Currency, itemsSummary,
            (int)record.PickupType,
            record.CarModel, record.CarColor, record.CarPlate,
            record.CustomerNotes, ct);

        return this.OkEnvelope("order.create", new
        {
            record.Id,
            record.OrderNumber,
            record.Subtotal,
            record.Total,
            record.Currency,
            record.PickupType,
            record.PreferredPayment,
            record.CashTendered,
            ExpectedChange = record.ExpectedChange,
            VendorName = vendor.Name,
            Status = record.Status.ToString()
        });
    }

    [HttpGet("by-customer/{customerId:guid}")]
    public async Task<IActionResult> ByCustomer(Guid customerId, CancellationToken ct)
    {
        var orders = await _orders.GetAllWithPredicateAsync(o => o.CustomerId == customerId);
        return await MapOrderList(orders, ct);
    }

    [HttpGet("by-vendor/{vendorId:guid}")]
    public async Task<IActionResult> ByVendor(Guid vendorId, CancellationToken ct)
    {
        var orders = await _orders.GetAllWithPredicateAsync(o => o.VendorId == vendorId);
        return await MapOrderList(orders, ct);
    }

    private async Task<IActionResult> MapOrderList(IReadOnlyList<OrderRecord> orders, CancellationToken ct)
    {
        var sorted = orders.OrderByDescending(o => o.CreatedAt).ToList();
        var vendors = (await _vendors.ListAllAsync(ct)).ToDictionary(v => v.Id);
        var users = (await _users.ListAllAsync(ct)).ToDictionary(u => u.Id);
        var result = sorted.Select(o => new
        {
            o.Id,
            o.OrderNumber,
            o.Total,
            o.Currency,
            o.Status,
            StatusAr = StatusToArabic(o.Status),
            o.PickupType,
            o.PreferredPayment,
            o.CashTendered,
            ExpectedChange = o.ExpectedChange,
            o.CreatedAt,
            VendorName = vendors.TryGetValue(o.VendorId, out var v) ? v.Name : "(محذوف)",
            VendorEmoji = vendors.TryGetValue(o.VendorId, out var v2) ? v2.LogoEmoji : "🏪",
            CustomerName = users.TryGetValue(o.CustomerId, out var u) ? (u.FullName ?? u.PhoneNumber) : "",
            CustomerPhone = users.TryGetValue(o.CustomerId, out var u2) ? u2.PhoneNumber : "",
        }).ToList();
        return this.OkEnvelope("order.list", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        var items = await _items.GetAllWithPredicateAsync(i => i.OrderId == id);
        var vendor = await _vendors.GetByIdAsync(o.VendorId, ct);
        var customer = await _users.GetByIdAsync(o.CustomerId, ct);
        return this.OkEnvelope("order.get", new
        {
            o.Id,
            o.OrderNumber,
            o.Total,
            o.Subtotal,
            o.Currency,
            o.Status,
            StatusAr = StatusToArabic(o.Status),
            o.PickupType,
            o.PreferredPayment,
            o.CashTendered,
            ExpectedChange = o.ExpectedChange,
            o.CarModel,
            o.CarColor,
            o.CarPlate,
            o.CustomerNotes,
            o.CreatedAt,
            CustomerName = customer?.FullName ?? customer?.PhoneNumber ?? "",
            CustomerPhone = customer?.PhoneNumber ?? "",
            Vendor = vendor == null ? null : new { vendor.Id, vendor.Name, vendor.Phone, vendor.LogoEmoji },
            Items = items.OrderBy(i => i.CreatedAt).Select(i => new
            {
                i.Id, i.OfferTitle, i.Emoji, i.Quantity, i.UnitPrice, i.LineTotal
            })
        });
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        if (o.Status != OrderStatus.Pending)
            return this.BadRequestEnvelope("not_pending", "الطلب ليس في حالة انتظار");

        var vendor = await _vendors.GetByIdAsync(o.VendorId, ct);

        var op = Entry.Create("order.accept")
            .Describe($"Vendor:{o.VendorId} accepts Order {o.OrderNumber}")
            .From($"Vendor:{o.VendorId}", 1, ("role", "acceptor"))
            .To($"Order:{o.Id}", 1, ("role", "order"))
            .Tag("order_number", o.OrderNumber)
            .Tag("vendor_name", vendor?.Name ?? "")
            .Execute(async ctx =>
            {
                o.Status = OrderStatus.Accepted;
                o.UpdatedAt = DateTime.UtcNow;
                await _orders.UpdateAsync(o, ctx.CancellationToken);
                ctx.Set("orderId", o.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { id = o.Id, status = "Accepted" }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);
        return this.OkEnvelope("order.accept", new { id = o.Id, status = o.Status.ToString() });
    }

    [HttpPost("{id:guid}/ready")]
    public async Task<IActionResult> Ready(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        if (o.Status != OrderStatus.Accepted)
            return this.BadRequestEnvelope("not_accepted", "يجب قبول الطلب أولاً");

        var op = Entry.Create("order.ready")
            .Describe($"Order {o.OrderNumber} is ready for pickup")
            .From($"Vendor:{o.VendorId}", 1, ("role", "preparer"))
            .To($"Order:{o.Id}", 1, ("role", "order"))
            .Tag("order_number", o.OrderNumber)
            .Tag("pickup_type", o.PickupType.ToString())
            .Execute(async ctx =>
            {
                o.Status = OrderStatus.Ready;
                o.UpdatedAt = DateTime.UtcNow;
                await _orders.UpdateAsync(o, ctx.CancellationToken);
                ctx.Set("orderId", o.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { id = o.Id, status = "Ready" }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);
        return this.OkEnvelope("order.ready", new { id = o.Id, status = o.Status.ToString() });
    }

    [HttpPost("{id:guid}/deliver")]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        if (o.Status != OrderStatus.Ready)
            return this.BadRequestEnvelope("not_ready", "الطلب ليس جاهزاً بعد");

        var op = Entry.Create("order.deliver")
            .Describe($"Order {o.OrderNumber} delivered to User:{o.CustomerId}")
            .From($"Order:{o.Id}", o.Total, ("role", "order"), ("currency", o.Currency))
            .To($"User:{o.CustomerId}", o.Total, ("role", "customer"), ("currency", o.Currency))
            .Tag("order_number", o.OrderNumber)
            .Tag("payment_method", o.PreferredPayment.ToString())
            .Execute(async ctx =>
            {
                o.Status = OrderStatus.Delivered;
                o.UpdatedAt = DateTime.UtcNow;
                await _orders.UpdateAsync(o, ctx.CancellationToken);
                ctx.Set("orderId", o.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { id = o.Id, status = "Delivered" }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);
        return this.OkEnvelope("order.deliver", new { id = o.Id, status = o.Status.ToString() });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        if (o.Status != OrderStatus.Pending && o.Status != OrderStatus.Accepted)
            return this.BadRequestEnvelope("not_cancellable", "لا يمكن إلغاء الطلب في حالته الحالية");

        // Cancellation reverses the original entry: value flows back from vendor to customer
        var op = Entry.Create("order.cancel")
            .Describe($"Order {o.OrderNumber} cancelled — reversing debit")
            .From($"Vendor:{o.VendorId}", o.Total, ("role", "vendor"), ("currency", o.Currency))
            .To($"User:{o.CustomerId}", o.Total, ("role", "customer"), ("currency", o.Currency))
            .Tag("order_number", o.OrderNumber)
            .Tag("previous_status", o.Status.ToString())
            .Execute(async ctx =>
            {
                o.Status = OrderStatus.Cancelled;
                o.UpdatedAt = DateTime.UtcNow;
                await _orders.UpdateAsync(o, ctx.CancellationToken);
                ctx.Set("orderId", o.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { id = o.Id, status = "Cancelled" }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);
        return this.OkEnvelope("order.cancel", new { id = o.Id, status = o.Status.ToString() });
    }

    // ── Vendor.Api callback — the "payment gateway" response ─────────────
    public record VendorCallbackRequest(string Action);

    [HttpPost("{id:guid}/vendor-callback")]
    public async Task<IActionResult> VendorCallback(Guid id, [FromBody] VendorCallbackRequest req, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");

        var newStatus = req.Action switch
        {
            "accepted" => OrderStatus.Accepted,
            "rejected" or "timeout" => OrderStatus.Cancelled,
            "ready" => OrderStatus.Ready,
            "delivered" => OrderStatus.Delivered,
            _ => (OrderStatus?)null
        };

        if (!newStatus.HasValue)
            return this.BadRequestEnvelope("unknown_action", $"Unknown action: {req.Action}");

        var opType = $"order.vendor_{req.Action}";
        var op = Entry.Create(opType)
            .Describe($"Vendor callback: {req.Action} for Order {o.OrderNumber}")
            .From($"VendorApi:callback", 1, ("role", "vendor_service"))
            .To($"Order:{o.Id}", 1, ("role", "order"))
            .Tag("order_number", o.OrderNumber)
            .Tag("callback_action", req.Action)
            .Execute(async ctx =>
            {
                o.Status = newStatus.Value;
                o.UpdatedAt = DateTime.UtcNow;
                await _orders.UpdateAsync(o, ctx.CancellationToken);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { id = o.Id, status = newStatus.Value.ToString() }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);
        return this.OkEnvelope(opType, new { id = o.Id, status = o.Status.ToString() });
    }

    private static string StatusToArabic(OrderStatus s) => s switch
    {
        OrderStatus.Pending => "في الانتظار",
        OrderStatus.Accepted => "تم القبول",
        OrderStatus.Ready => "جاهز للاستلام",
        OrderStatus.Delivered => "تم التسليم",
        OrderStatus.Cancelled => "ملغي",
        _ => "غير معروف"
    };
}
