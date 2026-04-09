# ACommerce.Reviews

## نظرة عامة
وحدة التقييمات العامة. قابلة للربط بأي كيان في النظام (منتجات، بائعين، طلبات، إلخ).

## الموقع
`/Modules/ACommerce.Reviews`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## الكيانات (Entities)

### Review
تقييم قابل للربط بأي كيان:

```csharp
public class Review : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // نوع الكيان (Product, Vendor, Order, etc)
    public required string EntityType { get; set; }

    // معرف الكيان
    public required Guid EntityId { get; set; }

    // معرف المستخدم
    public required string UserId { get; set; }

    // التقييم (1-5)
    public required int Rating { get; set; }

    // العنوان والتعليق
    public string? Title { get; set; }
    public string? Comment { get; set; }

    // الإيجابيات والسلبيات
    public List<string> Pros { get; set; } = new();
    public List<string> Cons { get; set; } = new();

    // الصور المرفقة
    public List<string> Images { get; set; } = new();

    // معلومات التحقق
    public bool IsVerifiedPurchase { get; set; }
    public bool IsApproved { get; set; }
    public int HelpfulCount { get; set; }

    // رد البائع
    public string? VendorResponse { get; set; }
    public DateTime? VendorResponseAt { get; set; }

    // بيانات إضافية
    [NotMapped] public Dictionary<string, string> Metadata { get; set; } = new();
}
```

---

## DTOs

### CreateReviewDto
```csharp
public class CreateReviewDto
{
    public required string EntityType { get; set; }
    public required Guid EntityId { get; set; }
    public required string UserId { get; set; }
    public required int Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public List<string>? Pros { get; set; }
    public List<string>? Cons { get; set; }
    public List<string>? Images { get; set; }
}
```

### ReviewResponseDto
```csharp
public class ReviewResponseDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string UserId { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public List<string> Pros { get; set; }
    public List<string> Cons { get; set; }
    public List<string> Images { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public bool IsApproved { get; set; }
    public int HelpfulCount { get; set; }
    public string? VendorResponse { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## بنية الملفات
```
ACommerce.Reviews/
├── Entities/
│   └── Review.cs
└── DTOs/
    └── CreateReviewDto.cs    # CreateReviewDto + ReviewResponseDto
```

---

## مثال استخدام

### إضافة تقييم لمنتج
```csharp
var review = new Review
{
    EntityType = "Product",
    EntityId = productId,
    UserId = userId,
    Rating = 5,
    Title = "منتج ممتاز",
    Comment = "جودة عالية وسعر مناسب",
    Pros = new List<string> { "جودة عالية", "سعر مناسب" },
    Cons = new List<string> { "التغليف يحتاج تحسين" },
    IsVerifiedPurchase = true
};

await reviewRepository.CreateAsync(review);
```

### إضافة تقييم لبائع
```csharp
var review = new Review
{
    EntityType = "Vendor",
    EntityId = vendorId,
    UserId = userId,
    Rating = 4,
    Comment = "تعامل ممتاز وشحن سريع"
};

await reviewRepository.CreateAsync(review);
```

### رد البائع على التقييم
```csharp
review.VendorResponse = "شكراً لتقييمكم، نعمل على تحسين التغليف";
review.VendorResponseAt = DateTime.UtcNow;

await reviewRepository.UpdateAsync(review);
```

---

## ملاحظات تقنية

1. **Entity Agnostic**: قابل للربط بأي كيان عبر EntityType و EntityId
2. **Verified Purchase**: دعم التحقق من الشراء الفعلي
3. **Moderation**: دعم موافقة المشرف على التقييمات
4. **Vendor Response**: إمكانية رد البائع على التقييم
5. **Pros/Cons**: دعم قوائم الإيجابيات والسلبيات
6. **Image Attachments**: دعم إرفاق صور مع التقييم
7. **Helpful Votes**: نظام تصويت على فائدة التقييم
