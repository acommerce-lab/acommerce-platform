# ACommerce.Orders

## نظرة عامة | Overview

مكتبة `ACommerce.Orders` توفر نظام إدارة الطلبات الكامل مع دعم سير العمل (Workflow) المتقدم، وتتبع الحالات، وإدارة المدفوعات والشحن، والإرجاع والاستبدال.

This library provides a complete order management system with advanced workflow support, status tracking, payment and shipping management, returns and exchanges.

**المسار | Path:** `Sales/ACommerce.Orders`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)
**الاعتماديات | Dependencies:**
- ACommerce.SharedKernel.Abstractions
- ACommerce.SharedKernel.CQRS
- ACommerce.Catalog.Products
- ACommerce.Payments.Abstractions
- ACommerce.Shipping.Abstractions

---

## سير عمل الطلب | Order Workflow

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              Order Lifecycle                                     │
└─────────────────────────────────────────────────────────────────────────────────┘

                              ┌─────────┐
                              │ Pending │ ← إنشاء الطلب
                              └────┬────┘
                                   │
                    ┌──────────────┼──────────────┐
                    ↓              ↓              ↓
             ┌───────────┐  ┌───────────┐  ┌───────────┐
             │ Cancelled │  │AwaitingPay│  │ Confirmed │ ← (COD)
             └───────────┘  └─────┬─────┘  └─────┬─────┘
                                  │              │
                           ┌──────┴──────┐       │
                           ↓             ↓       │
                    ┌───────────┐ ┌───────────┐  │
                    │PaymentFail│ │   Paid    │──┘
                    └───────────┘ └─────┬─────┘
                                        │
                                        ↓
                                 ┌───────────┐
                                 │ Processing│ ← تجهيز الطلب
                                 └─────┬─────┘
                                       │
                                       ↓
                                 ┌───────────┐
                                 │  Shipped  │ ← الشحن
                                 └─────┬─────┘
                                       │
                            ┌──────────┼──────────┐
                            ↓          ↓          ↓
                     ┌───────────┐ ┌───────────┐ ┌───────────┐
                     │ Delivered │ │PartialDel │ │  Failed   │
                     └─────┬─────┘ └───────────┘ └───────────┘
                           │
                    ┌──────┴──────┐
                    ↓             ↓
             ┌───────────┐ ┌───────────┐
             │ Completed │ │ Returned  │
             └───────────┘ └───────────┘
```

---

## نموذج البيانات | Data Model

### Order Entity

```csharp
public class Order : IEntity<Guid>, IAuditableEntity, ISoftDeletable, IMultiTenantEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>
    /// رقم الطلب المقروء (ORD-2024-001234)
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// معرف العميل
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// البريد الإلكتروني للعميل
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// رقم هاتف العميل
    /// </summary>
    public string? CustomerPhone { get; set; }

    /// <summary>
    /// حالة الطلب
    /// </summary>
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    /// <summary>
    /// حالة الدفع
    /// </summary>
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;

    /// <summary>
    /// حالة الشحن
    /// </summary>
    public ShippingStatus ShippingStatus { get; private set; } = ShippingStatus.Pending;

    // Pricing
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public Guid CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    // Addresses
    public OrderAddress ShippingAddress { get; set; } = new();
    public OrderAddress? BillingAddress { get; set; }

    // Shipping
    public Guid? ShippingMethodId { get; set; }
    public string? ShippingMethodName { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CarrierName { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }

    // Payment
    public Guid? PaymentMethodId { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? PaymentTransactionId { get; set; }
    public DateTime? PaidAt { get; set; }

    // Coupon
    public string? CouponCode { get; set; }
    public Guid? CouponId { get; set; }

    // Notes
    public string? CustomerNote { get; set; }
    public string? InternalNote { get; set; }

    // Relations
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public ICollection<OrderPayment> Payments { get; set; } = new List<OrderPayment>();

    // Source
    public OrderSource Source { get; set; } = OrderSource.Web;
    public string? SourceReference { get; set; }

    // Audit & Soft Delete
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    // Domain Events
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(IDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    #region Status Transitions

    public Result ConfirmOrder()
    {
        if (Status != OrderStatus.Pending)
            return Result.Failure("لا يمكن تأكيد الطلب في هذه الحالة");

        Status = OrderStatus.Confirmed;
        AddStatusHistory("تم تأكيد الطلب");
        AddDomainEvent(new OrderConfirmedEvent(Id, OrderNumber));

        return Result.Success();
    }

    public Result MarkAsPaid(string transactionId)
    {
        if (PaymentStatus == PaymentStatus.Paid)
            return Result.Failure("الطلب مدفوع مسبقاً");

        PaymentStatus = PaymentStatus.Paid;
        PaymentTransactionId = transactionId;
        PaidAt = DateTime.UtcNow;

        if (Status == OrderStatus.AwaitingPayment)
            Status = OrderStatus.Paid;

        AddStatusHistory($"تم استلام الدفع - {transactionId}");
        AddDomainEvent(new OrderPaidEvent(Id, OrderNumber, Total));

        return Result.Success();
    }

    public Result StartProcessing()
    {
        if (Status != OrderStatus.Confirmed && Status != OrderStatus.Paid)
            return Result.Failure("لا يمكن بدء التجهيز في هذه الحالة");

        Status = OrderStatus.Processing;
        AddStatusHistory("بدأ تجهيز الطلب");
        AddDomainEvent(new OrderProcessingStartedEvent(Id, OrderNumber));

        return Result.Success();
    }

    public Result Ship(string trackingNumber, string carrierName, DateTime? estimatedDelivery = null)
    {
        if (Status != OrderStatus.Processing)
            return Result.Failure("لا يمكن شحن الطلب في هذه الحالة");

        Status = OrderStatus.Shipped;
        ShippingStatus = ShippingStatus.InTransit;
        TrackingNumber = trackingNumber;
        CarrierName = carrierName;
        ShippedAt = DateTime.UtcNow;
        EstimatedDeliveryDate = estimatedDelivery;

        AddStatusHistory($"تم شحن الطلب - رقم التتبع: {trackingNumber}");
        AddDomainEvent(new OrderShippedEvent(Id, OrderNumber, trackingNumber, carrierName));

        return Result.Success();
    }

    public Result MarkAsDelivered()
    {
        if (Status != OrderStatus.Shipped)
            return Result.Failure("لا يمكن تسليم الطلب في هذه الحالة");

        Status = OrderStatus.Delivered;
        ShippingStatus = ShippingStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;

        AddStatusHistory("تم تسليم الطلب");
        AddDomainEvent(new OrderDeliveredEvent(Id, OrderNumber));

        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != OrderStatus.Delivered)
            return Result.Failure("لا يمكن إكمال الطلب في هذه الحالة");

        Status = OrderStatus.Completed;
        AddStatusHistory("تم إكمال الطلب");
        AddDomainEvent(new OrderCompletedEvent(Id, OrderNumber));

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status == OrderStatus.Shipped ||
            Status == OrderStatus.Delivered ||
            Status == OrderStatus.Completed)
            return Result.Failure("لا يمكن إلغاء الطلب بعد الشحن");

        Status = OrderStatus.Cancelled;
        AddStatusHistory($"تم إلغاء الطلب - السبب: {reason}");
        AddDomainEvent(new OrderCancelledEvent(Id, OrderNumber, reason));

        return Result.Success();
    }

    public Result RequestReturn(string reason)
    {
        if (Status != OrderStatus.Delivered && Status != OrderStatus.Completed)
            return Result.Failure("لا يمكن طلب إرجاع إلا بعد التسليم");

        Status = OrderStatus.ReturnRequested;
        AddStatusHistory($"طلب إرجاع - السبب: {reason}");
        AddDomainEvent(new OrderReturnRequestedEvent(Id, OrderNumber, reason));

        return Result.Success();
    }

    #endregion

    #region Calculations

    public void CalculateTotals()
    {
        Subtotal = Items.Sum(i => i.LineTotal);
        TaxAmount = (Subtotal - DiscountAmount) * 0.15m; // 15% VAT
        Total = Subtotal - DiscountAmount + ShippingCost + TaxAmount;
    }

    #endregion

    #region Private Methods

    private void AddStatusHistory(string note)
    {
        StatusHistory.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = Id,
            Status = Status,
            Note = note,
            CreatedAt = DateTime.UtcNow
        });
    }

    #endregion
}
```

### OrderItem Entity

```csharp
public class OrderItem : IEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    // Product Reference
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantTitle { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }

    // Pricing
    public decimal UnitPrice { get; set; }
    public decimal? OriginalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => (UnitPrice * Quantity) - DiscountAmount;

    // Product Attributes at time of order
    public Dictionary<string, string>? Attributes { get; set; }

    // Fulfillment
    public int QuantityFulfilled { get; set; }
    public int QuantityRefunded { get; set; }
    public ItemFulfillmentStatus FulfillmentStatus { get; set; } = ItemFulfillmentStatus.Pending;
}
```

### OrderStatusHistory Entity

```csharp
public class OrderStatusHistory : IEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public OrderStatus Status { get; set; }
    public string? Note { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### OrderAddress Value Object

```csharp
public class OrderAddress
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string FormattedAddress => string.Join(", ",
        new[] { AddressLine1, AddressLine2, City, State, PostalCode, Country }
        .Where(s => !string.IsNullOrEmpty(s)));
}
```

---

## Enums

```csharp
public enum OrderStatus
{
    /// <summary>
    /// في انتظار التأكيد
    /// </summary>
    Pending = 0,

    /// <summary>
    /// في انتظار الدفع
    /// </summary>
    AwaitingPayment = 1,

    /// <summary>
    /// تم التأكيد
    /// </summary>
    Confirmed = 2,

    /// <summary>
    /// تم الدفع
    /// </summary>
    Paid = 3,

    /// <summary>
    /// قيد التجهيز
    /// </summary>
    Processing = 4,

    /// <summary>
    /// تم الشحن
    /// </summary>
    Shipped = 5,

    /// <summary>
    /// تم التسليم
    /// </summary>
    Delivered = 6,

    /// <summary>
    /// تم الإكمال
    /// </summary>
    Completed = 7,

    /// <summary>
    /// ملغي
    /// </summary>
    Cancelled = 8,

    /// <summary>
    /// فشل الدفع
    /// </summary>
    PaymentFailed = 9,

    /// <summary>
    /// طلب إرجاع
    /// </summary>
    ReturnRequested = 10,

    /// <summary>
    /// تم الإرجاع
    /// </summary>
    Returned = 11
}

public enum PaymentStatus
{
    Pending = 0,
    Processing = 1,
    Paid = 2,
    PartiallyPaid = 3,
    Failed = 4,
    Refunded = 5,
    PartiallyRefunded = 6
}

public enum ShippingStatus
{
    Pending = 0,
    Processing = 1,
    ReadyToShip = 2,
    InTransit = 3,
    OutForDelivery = 4,
    Delivered = 5,
    Failed = 6,
    Returned = 7
}

public enum OrderSource
{
    Web = 0,
    Mobile = 1,
    Api = 2,
    Admin = 3,
    POS = 4
}

public enum ItemFulfillmentStatus
{
    Pending = 0,
    PartiallyFulfilled = 1,
    Fulfilled = 2,
    Cancelled = 3
}
```

---

## الأحداث النطاقية | Domain Events

```csharp
public record OrderCreatedEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    decimal Total
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record OrderConfirmedEvent(Guid OrderId, string OrderNumber) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record OrderPaidEvent(
    Guid OrderId,
    string OrderNumber,
    decimal Amount
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record OrderShippedEvent(
    Guid OrderId,
    string OrderNumber,
    string TrackingNumber,
    string CarrierName
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record OrderDeliveredEvent(Guid OrderId, string OrderNumber) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record OrderCompletedEvent(Guid OrderId, string OrderNumber) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record OrderCancelledEvent(
    Guid OrderId,
    string OrderNumber,
    string Reason
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

---

## الأوامر | Commands

### CreateOrderCommand

```csharp
public record CreateOrderCommand(
    Guid CustomerId,
    string CustomerEmail,
    string? CustomerPhone,
    List<CreateOrderItemDto> Items,
    OrderAddressDto ShippingAddress,
    OrderAddressDto? BillingAddress,
    Guid ShippingMethodId,
    Guid PaymentMethodId,
    string? CouponCode = null,
    string? CustomerNote = null,
    OrderSource Source = OrderSource.Web
) : ICommand<Result<Guid>>;

public record CreateOrderItemDto(
    Guid ProductId,
    Guid? VariantId,
    int Quantity
);

public record OrderAddressDto(
    string FirstName,
    string LastName,
    string AddressLine1,
    string City,
    string Country,
    string CountryCode,
    string Phone,
    string? Company = null,
    string? AddressLine2 = null,
    string? State = null,
    string? PostalCode = null,
    string? Email = null
);
```

### UpdateOrderStatusCommand

```csharp
public record UpdateOrderStatusCommand(
    Guid OrderId,
    OrderStatus NewStatus,
    string? Note = null,
    // For shipping
    string? TrackingNumber = null,
    string? CarrierName = null,
    DateTime? EstimatedDelivery = null,
    // For payment
    string? TransactionId = null,
    // For cancellation
    string? CancellationReason = null
) : ICommand<Result>;
```

---

## الاستعلامات | Queries

### GetOrderByIdQuery

```csharp
public record GetOrderByIdQuery(Guid Id) : IQuery<OrderDetailDto?>;

public class OrderDetailDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public ShippingStatus ShippingStatus { get; set; }

    // Customer
    public Guid CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }

    // Pricing
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    // Items
    public List<OrderItemDto> Items { get; set; } = new();

    // Addresses
    public OrderAddressDto ShippingAddress { get; set; } = new();
    public OrderAddressDto? BillingAddress { get; set; }

    // Shipping
    public string? ShippingMethodName { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CarrierName { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }

    // Payment
    public string? PaymentMethodName { get; set; }
    public DateTime? PaidAt { get; set; }

    // History
    public List<OrderStatusHistoryDto> StatusHistory { get; set; } = new();

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

### SearchOrdersQuery

```csharp
public record SearchOrdersQuery(
    Guid? CustomerId = null,
    OrderStatus? Status = null,
    PaymentStatus? PaymentStatus = null,
    ShippingStatus? ShippingStatus = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? SearchTerm = null,
    decimal? MinTotal = null,
    decimal? MaxTotal = null,
    string? SortBy = null,
    bool SortDescending = true,
    int Page = 1,
    int PageSize = 20
) : IQuery<SmartSearchResult<OrderListDto>>;
```

---

## خدمة رقم الطلب | Order Number Service

```csharp
public interface IOrderNumberService
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}

public class OrderNumberService : IOrderNumberService
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly OrderNumberOptions _options;

    public OrderNumberService(
        IRepository<Order, Guid> orderRepository,
        IOptions<OrderNumberOptions> options)
    {
        _orderRepository = orderRepository;
        _options = options.Value;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = _options.Prefix; // "ORD"

        // Get the last order number for this year
        var lastOrder = await _orderRepository
            .FindAsync(o => o.OrderNumber.StartsWith($"{prefix}-{year}-"), cancellationToken);

        var lastNumber = lastOrder
            .Select(o => int.TryParse(o.OrderNumber.Split('-').Last(), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        var newNumber = lastNumber + 1;

        return $"{prefix}-{year}-{newNumber:D6}"; // ORD-2024-000001
    }
}

public class OrderNumberOptions
{
    public string Prefix { get; set; } = "ORD";
}
```

---

## تسجيل الخدمات | Service Registration

```csharp
public static class OrdersServiceCollectionExtensions
{
    public static IServiceCollection AddOrders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<OrderNumberOptions>(
            configuration.GetSection("Orders:NumberFormat"));

        // Register repositories
        services.AddScoped<IRepository<Order, Guid>, EfCoreRepository<Order, Guid>>();
        services.AddScoped<IRepository<OrderItem, Guid>, EfCoreRepository<OrderItem, Guid>>();

        // Register services
        services.AddScoped<IOrderNumberService, OrderNumberService>();
        services.AddScoped<IOrderService, OrderService>();

        // Register CQRS
        services.AddCqrs(typeof(CreateOrderCommand).Assembly);

        // Register domain event handlers
        services.AddScoped<INotificationHandler<OrderCreatedEvent>, OrderCreatedEventHandler>();
        services.AddScoped<INotificationHandler<OrderShippedEvent>, OrderShippedEventHandler>();

        return services;
    }
}
```

---

## معالجات الأحداث | Event Handlers

```csharp
public class OrderCreatedEventHandler : INotificationHandler<OrderCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public async Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Order created: {OrderNumber}",
            notification.OrderNumber);

        // Send confirmation email
        // Notify inventory service
        // etc.
    }
}

public class OrderShippedEventHandler : INotificationHandler<OrderShippedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrderShippedEventHandler> _logger;

    public async Task Handle(OrderShippedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Order shipped: {OrderNumber}, Tracking: {TrackingNumber}",
            notification.OrderNumber,
            notification.TrackingNumber);

        // Send shipping notification
        await _notificationService.SendAsync(new ShippingNotification
        {
            OrderNumber = notification.OrderNumber,
            TrackingNumber = notification.TrackingNumber,
            CarrierName = notification.CarrierName
        }, cancellationToken);
    }
}
```

---

## المراجع | References

- [Order Management Best Practices](https://www.shopify.com/blog/order-management)
- [Domain-Driven Design](https://martinfowler.com/bliki/DomainDrivenDesign.html)
- [Event Sourcing Pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)
