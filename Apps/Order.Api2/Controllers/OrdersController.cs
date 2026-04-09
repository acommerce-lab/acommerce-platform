using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api2.Entities;

namespace Order.Api2.Controllers;

/// <summary>
/// إدارة طلبات اوردر. الإنشاء يمر عبر OperationEngine كقيد محاسبي:
/// العميل (مدين) ← التاجر (دائن) ببنود الطلب. لا دفع إلكتروني، استلام في
/// المتجر أو من السيارة فقط.
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

    public OrdersController(IRepositoryFactory factory, OpEngine engine)
    {
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
        var sorted = orders.OrderByDescending(o => o.CreatedAt).ToList();
        var vendors = (await _vendors.ListAllAsync(ct)).ToDictionary(v => v.Id);
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
            VendorEmoji = vendors.TryGetValue(o.VendorId, out var v2) ? v2.LogoEmoji : "🏪"
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
            Vendor = vendor == null ? null : new { vendor.Id, vendor.Name, vendor.Phone, vendor.LogoEmoji },
            Items = items.OrderBy(i => i.CreatedAt).Select(i => new
            {
                i.Id, i.OfferTitle, i.Emoji, i.Quantity, i.UnitPrice, i.LineTotal
            })
        });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("order_not_found");
        if (o.Status != OrderStatus.Pending && o.Status != OrderStatus.Accepted)
            return this.BadRequestEnvelope("not_cancellable", "لا يمكن إلغاء الطلب في حالته الحالية");
        o.Status = OrderStatus.Cancelled;
        o.UpdatedAt = DateTime.UtcNow;
        await _orders.UpdateAsync(o, ct);
        return this.OkEnvelope("order.cancel", new { id = o.Id, status = o.Status.ToString() });
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
