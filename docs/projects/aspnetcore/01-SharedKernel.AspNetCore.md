# ACommerce.SharedKernel.AspNetCore

## نظرة عامة
مكتبة ASP.NET Core مشتركة. توفر Base Controllers جاهزة للاستخدام مع CRUD operations و Middleware للأخطاء.

## الموقع
`/AspNetCore/ACommerce.SharedKernel.AspNetCore`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`
- `ACommerce.SharedKernel.CQRS`
- `MediatR`
- `Microsoft.AspNetCore`

---

## Base Controllers

### BaseCrudController
المتحكم الأساسي الكامل (CRUD):

```csharp
[ApiController]
[Route("api/[controller]")]
public abstract class BaseCrudController<TEntity, TCreateDto, TUpdateDto, TResponseDto, TPartialUpdateDto>
    : ControllerBase
    where TEntity : class, IBaseEntity
```

#### نقاط النهاية الموروثة

| Method | Endpoint | الوصف |
|--------|----------|-------|
| `GET` | `/api/{controller}/{id}` | جلب كيان بالمعرف |
| `POST` | `/api/{controller}/search` | البحث الذكي |
| `GET` | `/api/{controller}/count` | عد الكيانات |
| `POST` | `/api/{controller}` | إنشاء كيان |
| `PUT` | `/api/{controller}/{id}` | تحديث كامل |
| `PATCH` | `/api/{controller}/{id}` | تحديث جزئي |
| `DELETE` | `/api/{controller}/{id}` | حذف (soft/hard) |
| `POST` | `/api/{controller}/{id}/restore` | استعادة محذوف |

#### مثال الاستخدام

```csharp
public class ProductsController : BaseCrudController<
    Product,           // Entity
    CreateProductDto,  // Create DTO
    UpdateProductDto,  // Update DTO
    ProductDto,        // Response DTO
    PatchProductDto>   // Partial Update DTO
{
    public ProductsController(IMediator mediator, ILogger<ProductsController> logger)
        : base(mediator, logger) { }

    // يمكن إضافة endpoints إضافية هنا
}
```

### BaseQueryController
للقراءة فقط:

```csharp
public abstract class BaseQueryController<TEntity, TResponseDto> : ControllerBase
```

### BaseCommandController
للكتابة فقط:

```csharp
public abstract class BaseCommandController<TEntity, TCreateDto, TUpdateDto> : ControllerBase
```

---

## Middleware

### GlobalExceptionMiddleware
معالجة الأخطاء العامة:

```csharp
public class GlobalExceptionMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // تسجيل الخطأ وإرجاع response مناسب
        }
    }
}
```

---

## Extensions

### ApplicationBuilderExtensions

```csharp
public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
{
    return app.UseMiddleware<GlobalExceptionMiddleware>();
}
```

---

## بنية الملفات
```
ACommerce.SharedKernel.AspNetCore/
├── Controllers/
│   ├── BaseCrudController.cs
│   ├── BaseQueryController.cs
│   └── BaseCommandController.cs
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
└── Extensions/
    └── ApplicationBuilderExtensions.cs
```

---

## ميزات BaseCrudController

1. **SmartSearch**: بحث متقدم مع فلترة وترتيب وتصفح
2. **Soft Delete**: حذف منطقي مع إمكانية الاستعادة
3. **Validation**: معالجة FluentValidation
4. **Logging**: تسجيل شامل للعمليات
5. **Error Handling**: معالجة موحدة للأخطاء
6. **Include Properties**: دعم تحميل العلاقات

---

## مثال Response

### خطأ Validation
```json
{
  "message": "Validation failed",
  "errors": [
    { "field": "Name", "message": "Name is required" },
    { "field": "Price", "message": "Price must be greater than 0" }
  ]
}
```

### خطأ عام
```json
{
  "message": "An error occurred while processing your request",
  "detail": "Error details..."
}
```

---

## ملاحظات تقنية

1. **Generic Controllers**: controllers قابلة لإعادة الاستخدام
2. **CQRS Integration**: تكامل مع Commands و Queries
3. **MediatR**: يستخدم MediatR للـ dispatch
4. **OpenAPI**: دعم ProducesResponseType
5. **Async/Await**: جميع العمليات async
