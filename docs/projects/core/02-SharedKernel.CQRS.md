# ACommerce.SharedKernel.CQRS

## نظرة عامة
مكتبة تنفيذ نمط CQRS (Command Query Responsibility Segregation) باستخدام MediatR مع دعم AutoMapper و FluentValidation. توفر معالجات جاهزة وسلوكيات Pipeline قابلة للتوسيع.

## الموقع
`/Core/ACommerce.SharedKernel.CQRS`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`
- `MediatR`
- `AutoMapper`
- `FluentValidation`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`

---

## الأوامر (Commands)

### CreateCommand
أمر إنشاء كيان جديد:
```csharp
public class CreateCommand<TEntity, TDto> : IRequest<TEntity>
    where TEntity : class, IBaseEntity
{
    public required TDto Data { get; set; }
}
```

### UpdateCommand
أمر تحديث كيان كامل:
```csharp
public class UpdateCommand<TEntity, TDto> : IRequest<Unit>
    where TEntity : class, IBaseEntity
{
    public Guid Id { get; set; }
    public required TDto Data { get; set; }
}
```

### PartialUpdateCommand
أمر تحديث جزئي (الحقول المقدمة فقط):
```csharp
public class PartialUpdateCommand<TEntity, TDto> : IRequest<Unit>
    where TEntity : class, IBaseEntity
{
    public Guid Id { get; set; }
    public required TDto Data { get; set; }
}
```

### DeleteCommand
أمر حذف (منطقي أو فيزيائي):
```csharp
public class DeleteCommand<TEntity> : IRequest<Unit>
    where TEntity : class, IBaseEntity
{
    public Guid Id { get; set; }
    public bool SoftDelete { get; set; } = true; // افتراضي: حذف منطقي
}
```

### RestoreCommand
أمر استعادة كيان محذوف منطقياً:
```csharp
public class RestoreCommand<TEntity> : IRequest<Unit>
    where TEntity : class, IBaseEntity
{
    public Guid Id { get; set; }
}
```

---

## الاستعلامات (Queries)

### GetByIdQuery
استعلام للحصول على كيان بالمعرف:
```csharp
public class GetByIdQuery<TEntity, TDto> : IRequest<TDto?>
    where TEntity : class, IBaseEntity
{
    public Guid Id { get; set; }
    public List<string>? IncludeProperties { get; set; }  // Navigation Properties
    public bool IncludeDeleted { get; set; } = false;     // تضمين المحذوفات
}
```

### SmartSearchQuery
استعلام البحث الذكي مع الصفحات:
```csharp
public class SmartSearchQuery<TEntity, TDto> : IRequest<PagedResult<TDto>>
    where TEntity : class, IBaseEntity
{
    public SmartSearchRequest Request { get; set; } = new();
}
```

---

## المعالجات (Handlers)

### CreateCommandHandler
- يستخدم AutoMapper لتحويل DTO إلى Entity
- يضيف الكيان عبر Repository
- يسجل العملية في Log
- يرجع الكيان المُنشأ

### UpdateCommandHandler
- يجلب الكيان من قاعدة البيانات
- يرمي `EntityNotFoundException` إذا لم يوجد
- يطبق التغييرات عبر AutoMapper
- يحفظ التعديلات

### PartialUpdateCommandHandler
- مشابه لـ UpdateCommandHandler
- يطبق فقط الحقول غير الـ null

### DeleteCommandHandler
- يتحقق من وجود الكيان
- يدعم الحذف المنطقي (SoftDelete) والفيزيائي
- `SoftDelete = true` (افتراضي): يضع `IsDeleted = true`
- `SoftDelete = false`: حذف نهائي من قاعدة البيانات

### RestoreCommandHandler
- يستعيد كيان محذوف منطقياً
- يضع `IsDeleted = false`

### GetByIdQueryHandler
- يجلب الكيان بالمعرف
- يحول إلى DTO عبر AutoMapper
- يرجع `null` إذا لم يوجد

### SmartSearchQueryHandler
- يتحقق من صحة الطلب (`IsValid()`)
- يرجع نتيجة فارغة إذا كان الطلب غير صالح
- يبحث عبر Repository
- يحول النتائج إلى DTOs
- يرجع `PagedResult<TDto>`

---

## السلوكيات (Behaviors)

### ValidationBehavior
سلوك للتحقق التلقائي من الصحة باستخدام FluentValidation:
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
```
- يجمع جميع الـ Validators المسجلة للـ Request
- ينفذها بالتوازي (`Task.WhenAll`)
- يرمي `ValidationException` إذا فشل التحقق
- يسجل الأخطاء في Log

### PerformanceBehavior
سلوك لتتبع الأداء:
```csharp
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
```
- يقيس وقت تنفيذ كل Request
- يحذر إذا تجاوز الحد (500ms افتراضياً)
- يسجل في Log كـ `[PERFORMANCE]`

### LoggingBehavior
سلوك للتسجيل التلقائي:
```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
```
- يسجل `[START]` مع GUID فريد
- يقيس وقت التنفيذ
- يسجل `[END]` عند النجاح
- يسجل `[ERROR]` عند حدوث استثناء

---

## تسجيل الخدمات

### AddSharedKernelCQRS
```csharp
public static IServiceCollection AddSharedKernelCQRS(
    this IServiceCollection services,
    params Assembly[] assemblies)
```

**ما يفعله:**
1. **تحميل مكتبات ACommerce**: يبحث في دليل التطبيق عن ملفات `ACommerce.*.dll` ويحملها
2. **تسجيل MediatR** مع:
   - `LoggingBehavior`
   - `ValidationBehavior`
   - `PerformanceBehavior`
3. **اكتشاف تلقائي للـ Handlers**: يبحث عن Entities و DTOs ويسجل معالجاتها
4. **تسجيل AutoMapper** مع `ConventionMappingProfile`
5. **تسجيل FluentValidation**

### AddEntityHandlers
```csharp
public static IServiceCollection AddEntityHandlers<TEntity, TCreateDto, TUpdateDto, TResponseDto, TPartialUpdateDto>(
    this IServiceCollection services)
```

يسجل معالجات CQRS يدوياً لكيان معين مع جميع أنواع DTOs.

---

## الـ Mapping التلقائي

### ConventionMappingProfile
Profile يعتمد على اصطلاحات تسمية DTOs:

| نمط DTO | اتجاه التحويل | الحقول المُتجاهلة |
|---------|---------------|-------------------|
| `Create{Entity}Dto` | DTO → Entity | Id, CreatedAt, UpdatedAt, IsDeleted |
| `Update{Entity}Dto` | DTO → Entity | Id, CreatedAt, UpdatedAt, IsDeleted |
| `PartialUpdate{Entity}Dto` | DTO → Entity (null فقط) | Id, CreatedAt, UpdatedAt, IsDeleted |
| `{Entity}ResponseDto` | Entity ↔ DTO | لا شيء (ثنائي الاتجاه) |

---

## مثال الاستخدام

### التسجيل في Program.cs
```csharp
builder.Services.AddSharedKernelCQRS(typeof(Program).Assembly);
```

### إنشاء كيان
```csharp
var command = new CreateCommand<Product, CreateProductDto>
{
    Data = new CreateProductDto
    {
        Name = "منتج جديد",
        Price = 99.99m
    }
};

var product = await mediator.Send(command);
```

### تحديث كيان
```csharp
var command = new UpdateCommand<Product, UpdateProductDto>
{
    Id = productId,
    Data = new UpdateProductDto
    {
        Name = "اسم محدث",
        Price = 149.99m
    }
};

await mediator.Send(command);
```

### حذف منطقي
```csharp
var command = new DeleteCommand<Product>
{
    Id = productId,
    SoftDelete = true  // افتراضي
};

await mediator.Send(command);
```

### استعادة محذوف
```csharp
var command = new RestoreCommand<Product>
{
    Id = productId
};

await mediator.Send(command);
```

### بحث ذكي
```csharp
var query = new SmartSearchQuery<Product, ProductResponseDto>
{
    Request = new SmartSearchRequest
    {
        SearchTerm = "هاتف",
        PageNumber = 1,
        PageSize = 20,
        Filters = new List<FilterItem>
        {
            new() { PropertyName = "Price", Operator = FilterOperator.LessThan, Value = 1000 }
        },
        OrderBy = "Price",
        Ascending = true
    }
};

var result = await mediator.Send(query);
// result.Items, result.TotalCount, result.HasNextPage
```

---

## بنية الملفات
```
ACommerce.SharedKernel.CQRS/
├── Commands/
│   ├── CreateCommand.cs
│   ├── UpdateCommand.cs
│   ├── PartialUpdateCommand.cs
│   ├── DeleteCommand.cs
│   └── RestoreCommand.cs
├── Queries/
│   ├── GetByIdQuery.cs
│   └── SmartSearchQuery.cs
├── Handlers/
│   ├── CreateCommandHandler.cs
│   ├── UpdateCommandHandler.cs
│   ├── PartialUpdateCommandHandler.cs
│   ├── DeleteCommandHandler.cs
│   ├── RestoreCommandHandler.cs
│   ├── GetByIdQueryHandler.cs
│   └── SmartSearchQueryHandler.cs
├── Behaviors/
│   ├── LoggingBehavior.cs
│   ├── ValidationBehavior.cs
│   └── PerformanceBehavior.cs
├── Mapping/
│   └── ConventionMappingProfile.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

---

## ملاحظات تقنية

1. **Generic Handlers**: جميع المعالجات generic وتعمل مع أي Entity يطبق `IBaseEntity`
2. **Null-Safe Mapping**: الـ PartialUpdate يتجاهل الحقول `null` تلقائياً
3. **Auto-Discovery**: النظام يكتشف الـ Entities و DTOs تلقائياً ويسجل معالجاتها
4. **Exception Handling**: الـ LoggingBehavior يلتقط الاستثناءات ويسجلها قبل إعادة رميها
5. **Performance Threshold**: يمكن تعديل حد الـ 500ms للتحذير من العمليات البطيئة
