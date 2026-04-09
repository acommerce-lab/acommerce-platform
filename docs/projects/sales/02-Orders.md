# ACommerce.Orders

## نظرة عامة
مكتبة إدارة الطلبات. تدعم الطلبات متعددة البائعين مع تتبع العمولات والحالات المتقدمة.

## الموقع
`/Sales/ACommerce.Orders`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## الكيانات (Entities)

### Order
الطلب الرئيسي:

```csharp
public class Order : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // المعرفات
    public required string OrderNumber { get; set; }
    public required string CustomerId { get; set; }
    public Guid? VendorId { get; set; }  // null للطلبات متعددة البائعين

    // الحالة
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    // المبالغ
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CouponCode { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public required string Currency { get; set; }

    // العناوين
    public string? ShippingAddress { get; set; }
    public string? BillingAddress { get; set; }

    // الدفع والشحن
    public string? PaymentId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ShipmentId { get; set; }
    public string? TrackingNumber { get; set; }

    // الملاحظات
    public string? CustomerNotes { get; set; }
    public string? InternalNotes { get; set; }

    // البنود
    public List<OrderItem> Items { get; set; } = new();

    // التواريخ
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // بيانات إضافية
    [NotMapped]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
```

### OrderItem
بند في الطلب:

```csharp
public class OrderItem : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // الربط
    public Guid OrderId { get; set; }
    public Guid ListingId { get; set; }
    public Guid VendorId { get; set; }
    public Guid ProductId { get; set; }

    // بيانات المنتج (نسخة)
    public required string ProductName { get; set; }
    public string? Sku { get; set; }

    // الكميات والأسعار
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total => UnitPrice * Quantity;
    public decimal? ItemDiscount { get; set; }

    // العمولات
    public decimal CommissionAmount { get; set; }  // عمولة المنصة
    public decimal VendorAmount { get; set; }      // صافي البائع

    [NotMapped]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
```

---

## التعدادات (Enums)

### OrderStatus

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Draft` | 1 | مسودة (في السلة) |
| `Pending` | 2 | قيد الانتظار (بانتظار الدفع) |
| `Confirmed` | 3 | مؤكد (تم الدفع) |
| `Processing` | 4 | قيد التجهيز |
| `ReadyToShip` | 5 | جاهز للشحن |
| `Shipped` | 6 | تم الشحن |
| `OutForDelivery` | 7 | قيد التوصيل |
| `Delivered` | 8 | تم التسليم |
| `Cancelled` | 9 | ملغي |
| `Returned` | 10 | مرتجع |
| `Refunded` | 11 | مسترجع |

---

## DTOs

### CreateOrderDto
```csharp
public class CreateOrderDto
{
    public required string CustomerId { get; set; }
    public required List<OrderItemDto> Items { get; set; }
    public string? CouponCode { get; set; }
    public required string ShippingAddress { get; set; }
    public string? BillingAddress { get; set; }
    public string? CustomerNotes { get; set; }
}
```

### OrderItemDto
```csharp
public class OrderItemDto
{
    public Guid ListingId { get; set; }
    public int Quantity { get; set; }
}
```

### OrderResponseDto
```csharp
public class OrderResponseDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; }
    public string CustomerId { get; set; }
    public string Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; }
    public List<OrderItemResponseDto> Items { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### OrderItemResponseDto
```csharp
public class OrderItemResponseDto
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Guid ProductId { get; set; }
    public Guid VendorId { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}
```

---

## بنية الملفات
```
ACommerce.Orders/
├── Entities/
│   └── Order.cs        # Order + OrderItem
├── DTOs/
│   └── CreateOrderDto.cs  # جميع DTOs
└── Enums/
    └── OrderStatus.cs
```

---

## مثال استخدام

### إنشاء طلب
```csharp
var order = new Order
{
    OrderNumber = GenerateOrderNumber(),
    CustomerId = customerId,
    Currency = "SAR",
    Status = OrderStatus.Pending
};

foreach (var item in cartItems)
{
    order.Items.Add(new OrderItem
    {
        ListingId = item.ListingId,
        VendorId = listing.VendorId,
        ProductId = listing.ProductId,
        ProductName = product.Name,
        Quantity = item.Quantity,
        UnitPrice = listing.Price,
        CommissionAmount = CalculateCommission(listing.Price),
        VendorAmount = listing.Price - CalculateCommission(listing.Price)
    });
}

order.Subtotal = order.Items.Sum(i => i.Total);
order.Total = order.Subtotal + order.TaxAmount + order.ShippingCost - order.DiscountAmount;

await orderRepository.AddAsync(order);
```

### تحديث حالة الطلب
```csharp
order.Status = OrderStatus.Shipped;
order.ShippedAt = DateTime.UtcNow;
order.TrackingNumber = trackingNumber;

await orderRepository.UpdateAsync(order);
```

### حساب عمولة البائع
```csharp
// حساب المبلغ الصافي للبائع
var vendorOrders = order.Items
    .Where(i => i.VendorId == vendorId)
    .ToList();

var totalSales = vendorOrders.Sum(i => i.Total);
var totalCommission = vendorOrders.Sum(i => i.CommissionAmount);
var netAmount = vendorOrders.Sum(i => i.VendorAmount);
```

---

## ملاحظات تقنية

1. **Multi-Vendor Support**: يدعم الطلبات من بائعين متعددين
2. **Commission Tracking**: تتبع العمولات على مستوى البند
3. **Price Snapshot**: يحفظ اسم المنتج والسعر كنسخة
4. **Status Flow**: 11 حالة لتتبع دورة حياة الطلب
5. **Timestamp Tracking**: تواريخ منفصلة لكل مرحلة
6. **Listing Reference**: يربط بالعرض (Listing) وليس المنتج مباشرة
