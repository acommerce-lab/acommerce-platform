# ACommerce.Cart

## نظرة عامة
مكتبة سلة التسوق. تدعم المستخدمين المسجلين والضيوف مع كوبونات الخصم.

## الموقع
`/Sales/ACommerce.Cart`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## الكيانات (Entities)

### Cart
سلة التسوق:

```csharp
public class Cart : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // معرف المستخدم أو Session للضيوف
    public required string UserIdOrSessionId { get; set; }

    // بنود السلة
    public List<CartItem> Items { get; set; } = new();

    // الخصومات
    public string? CouponCode { get; set; }
    public decimal? DiscountAmount { get; set; }

    // ملاحظات
    public string? Notes { get; set; }

    // حساب المجموع
    public decimal GetTotal();
}
```

### CartItem
بند في السلة:

```csharp
public class CartItem : IBaseEntity
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public Guid ListingId { get; set; }  // عرض المنتج
    public int Quantity { get; set; }
    public decimal Price { get; set; }   // السعر عند الإضافة
}
```

---

## DTOs

### AddToCartDto
```csharp
public class AddToCartDto
{
    public required string UserIdOrSessionId { get; set; }
    public Guid ListingId { get; set; }
    public int Quantity { get; set; } = 1;
}
```

### UpdateCartItemDto
```csharp
public class UpdateCartItemDto
{
    public int Quantity { get; set; }
}
```

### CartResponseDto
```csharp
public class CartResponseDto
{
    public Guid Id { get; set; }
    public string UserIdOrSessionId { get; set; }
    public List<CartItemDto> Items { get; set; }
    public string? CouponCode { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
}
```

### CartItemDto
```csharp
public class CartItemDto
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total => Price * Quantity;
}
```

---

## بنية الملفات
```
ACommerce.Cart/
├── Entities/
│   └── Cart.cs         # Cart + CartItem
├── DTOs/
│   └── AddToCartDto.cs # جميع DTOs
└── Controllers/
    └── CartController.cs
```

---

## مثال استخدام

### إضافة منتج للسلة
```csharp
var cart = await cartRepository.GetByUserAsync(userId)
    ?? new Cart { UserIdOrSessionId = userId };

cart.Items.Add(new CartItem
{
    ListingId = listingId,
    Quantity = 2,
    Price = listing.Price
});

await cartRepository.UpdateAsync(cart);
```

### تطبيق كوبون
```csharp
cart.CouponCode = "SAVE20";
cart.DiscountAmount = cart.GetTotal() * 0.2m;

await cartRepository.UpdateAsync(cart);
```

---

## ملاحظات تقنية

1. **Guest Support**: يدعم الضيوف عبر SessionId
2. **Price Snapshot**: يحفظ السعر عند الإضافة
3. **Coupon Support**: دعم كوبونات الخصم
4. **Listing Reference**: يربط بـ ProductListing وليس Product مباشرة
