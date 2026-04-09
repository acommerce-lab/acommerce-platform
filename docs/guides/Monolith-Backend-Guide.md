# دليل إنشاء الباك اند المفرد | Monolith Backend Guide

## مقدمة | Introduction

هذا الدليل الشامل يشرح كيفية بناء تطبيق باك اند متكامل (Monolith) باستخدام مكتبات ACommerce. ستتعلم كيفية إنشاء API كامل للتجارة الإلكترونية مع جميع الميزات المطلوبة.

This comprehensive guide explains how to build a complete monolithic backend application using ACommerce libraries. You'll learn how to create a full e-commerce API with all required features.

---

## المتطلبات | Prerequisites

- .NET 9.0 SDK
- PostgreSQL أو SQL Server
- Visual Studio 2022 أو VS Code أو Rider
- Redis (اختياري - للتخزين المؤقت)

---

## الخطوة 1: إنشاء المشروع | Step 1: Create Project

### إنشاء Solution جديد

```bash
# إنشاء مجلد المشروع
mkdir MyEShop
cd MyEShop

# إنشاء Solution
dotnet new sln -n MyEShop

# إنشاء مشروع API
dotnet new webapi -n MyEShop.Api -o src/MyEShop.Api

# إضافة المشروع للـ Solution
dotnet sln add src/MyEShop.Api
```

### تثبيت حزم ACommerce

```bash
cd src/MyEShop.Api

# الحزم الأساسية
dotnet add package ACommerce.SharedKernel.Abstractions
dotnet add package ACommerce.SharedKernel.CQRS
dotnet add package ACommerce.SharedKernel.Infrastructure.EFCore

# المصادقة
dotnet add package ACommerce.Authentication.Abstractions
dotnet add package ACommerce.Authentication.JWT
dotnet add package ACommerce.Authentication.TwoFactor.SMS

# الكتالوج
dotnet add package ACommerce.Catalog.Products
dotnet add package ACommerce.Catalog.Categories
dotnet add package ACommerce.Catalog.Attributes
dotnet add package ACommerce.Catalog.Currencies

# المبيعات
dotnet add package ACommerce.Cart
dotnet add package ACommerce.Orders

# المدفوعات والشحن
dotnet add package ACommerce.Payments.Abstractions
dotnet add package ACommerce.Payments.Moyasar
dotnet add package ACommerce.Shipping.Abstractions

# قاعدة البيانات
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
# أو
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

---

## الخطوة 2: هيكل المشروع | Step 2: Project Structure

```
MyEShop/
├── src/
│   └── MyEShop.Api/
│       ├── Controllers/
│       │   ├── AuthController.cs
│       │   ├── ProductsController.cs
│       │   ├── CategoriesController.cs
│       │   ├── CartController.cs
│       │   ├── OrdersController.cs
│       │   └── PaymentsController.cs
│       ├── Data/
│       │   ├── AppDbContext.cs
│       │   ├── Configurations/
│       │   │   ├── ProductConfiguration.cs
│       │   │   ├── CategoryConfiguration.cs
│       │   │   └── OrderConfiguration.cs
│       │   └── Migrations/
│       ├── Services/
│       │   ├── CurrentUserService.cs
│       │   └── TenantService.cs
│       ├── Extensions/
│       │   └── ServiceCollectionExtensions.cs
│       ├── Middleware/
│       │   └── TenantMiddleware.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── Program.cs
├── tests/
│   └── MyEShop.Api.Tests/
└── MyEShop.sln
```

---

## الخطوة 3: إعداد DbContext | Step 3: Setup DbContext

### AppDbContext.cs

```csharp
using ACommerce.SharedKernel.Infrastructure.EFCore;
using ACommerce.Catalog.Products;
using ACommerce.Catalog.Categories;
using ACommerce.Orders;
using ACommerce.Cart;

namespace MyEShop.Api.Data;

public class AppDbContext : ACommerceDbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantService? tenantService = null,
        ICurrentUserService? currentUserService = null)
        : base(options, tenantService, currentUserService)
    {
    }

    // Catalog
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();

    // Sales
    public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // Authentication
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Apply configurations from ACommerce libraries
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Product).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Order).Assembly);
    }
}
```

---

## الخطوة 4: تكوين الخدمات | Step 4: Configure Services

### ServiceCollectionExtensions.cs

```csharp
using ACommerce.SharedKernel.CQRS;
using ACommerce.SharedKernel.Infrastructure.EFCore;
using ACommerce.Authentication.JWT;
using ACommerce.Catalog.Products;
using ACommerce.Orders;
using ACommerce.Payments.Moyasar;

namespace MyEShop.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDatabase(configuration);

        // Authentication
        services.AddAuthenticationServices(configuration);

        // CQRS
        services.AddCqrsServices();

        // Domain Services
        services.AddDomainServices();

        // Payments
        services.AddPaymentServices(configuration);

        // HTTP Context
        services.AddHttpContextAccessor();

        return services;
    }

    private static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(3);
            });

            // Enable detailed errors in development
            #if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
            #endif
        });

        // Register as IUnitOfWork
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Register generic repositories
        services.AddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));
        services.AddScoped(typeof(IReadOnlyRepository<,>), typeof(EfCoreRepository<,>));

        return services;
    }

    private static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // JWT Authentication
        services.AddJwtAuthentication(configuration);

        // Current User Service
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Tenant Service (for multi-tenancy)
        services.AddScoped<ITenantService, TenantService>();

        return services;
    }

    private static IServiceCollection AddCqrsServices(
        this IServiceCollection services)
    {
        // Register MediatR with all command/query handlers
        services.AddCqrsWithTransaction(
            typeof(CreateProductCommand).Assembly,
            typeof(CreateOrderCommand).Assembly,
            typeof(Program).Assembly // Local handlers
        );

        return services;
    }

    private static IServiceCollection AddDomainServices(
        this IServiceCollection services)
    {
        // Product Services
        services.AddCatalogProducts();

        // Order Services
        services.AddOrders(services.BuildServiceProvider()
            .GetRequiredService<IConfiguration>());

        // Cart Services
        services.AddScoped<ICartService, CartService>();

        // Slug Service
        services.AddScoped<ISlugService, SlugService>();

        // Order Number Service
        services.AddScoped<IOrderNumberService, OrderNumberService>();

        return services;
    }

    private static IServiceCollection AddPaymentServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Moyasar Payments
        services.AddMoyasarPayments(configuration);

        // Payment Provider Factory
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();

        return services;
    }
}
```

---

## الخطوة 5: Program.cs

```csharp
using MyEShop.Api.Data;
using MyEShop.Api.Extensions;
using MyEShop.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddApplicationServices(builder.Configuration);

// Add controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MyEShop API",
        Version = "v1",
        Description = "E-commerce API built with ACommerce libraries"
    });

    // JWT Authentication in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Tenant middleware (for multi-tenancy)
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

app.Run();
```

---

## الخطوة 6: إنشاء Controllers | Step 6: Create Controllers

### AuthController.cs

```csharp
using ACommerce.Authentication.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyEShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IPasswordService _passwordService;

    public AuthController(
        IAuthenticationService authService,
        IPasswordService passwordService)
    {
        _authService = authService;
        _passwordService = passwordService;
    }

    /// <summary>
    /// تسجيل مستخدم جديد
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResult>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return CreatedAtAction(nameof(Login), result);
    }

    /// <summary>
    /// تسجيل الدخول
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResult>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        if (!result.Succeeded)
        {
            if (result.RequiresTwoFactor)
                return Ok(new { requiresTwoFactor = true, provider = result.TwoFactorProvider });

            return Unauthorized(new { error = result.Error });
        }

        return Ok(result);
    }

    /// <summary>
    /// تحديث التوكن
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResult>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);

        if (!result.Succeeded)
            return Unauthorized(new { error = result.Error });

        return Ok(result);
    }

    /// <summary>
    /// تسجيل الخروج
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return NoContent();
    }

    /// <summary>
    /// طلب إعادة تعيين كلمة المرور
    /// </summary>
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _passwordService.RequestPasswordResetAsync(request.Email);

        // Always return success to prevent email enumeration
        return Ok(new { message = "إذا كان البريد الإلكتروني موجوداً، سيتم إرسال رابط إعادة التعيين" });
    }

    /// <summary>
    /// إعادة تعيين كلمة المرور
    /// </summary>
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _passwordService.ResetPasswordAsync(
            request.Email,
            request.Token,
            request.NewPassword);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(new { message = "تم إعادة تعيين كلمة المرور بنجاح" });
    }
}

// DTOs
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
```

### ProductsController.cs

```csharp
using ACommerce.Catalog.Products;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyEShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// البحث في المنتجات
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SmartSearchResult<ProductListDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] Guid? category,
        [FromQuery] Guid? brand,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] bool? inStock,
        [FromQuery] string? sort,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var query = new SearchProductsQuery(
            SearchTerm: q,
            CategoryId: category,
            BrandId: brand,
            MinPrice: minPrice,
            MaxPrice: maxPrice,
            InStock: inStock,
            SortBy: sort,
            SortDescending: desc,
            Page: page,
            PageSize: size);

        var result = await _mediator.Send(query);

        return Ok(result);
    }

    /// <summary>
    /// الحصول على منتج بالمعرف
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailDto>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(id));

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// الحصول على منتج بالـ Slug
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<ProductDetailDto>> GetBySlug(string slug)
    {
        var result = await _mediator.Send(new GetProductBySlugQuery(slug));

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// إنشاء منتج جديد (للمشرفين)
    /// </summary>
    [Authorize(Roles = "Admin,ProductManager")]
    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProductCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value },
            new { id = result.Value });
    }

    /// <summary>
    /// تحديث منتج (للمشرفين)
    /// </summary>
    [Authorize(Roles = "Admin,ProductManager")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command)
    {
        if (id != command.Id)
            return BadRequest(new { error = "ID mismatch" });

        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    /// <summary>
    /// حذف منتج (للمشرفين)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteProductCommand(id));

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    /// <summary>
    /// المنتجات ذات الصلة
    /// </summary>
    [HttpGet("{id:guid}/related")]
    public async Task<ActionResult<List<ProductListDto>>> GetRelated(Guid id, [FromQuery] int count = 6)
    {
        var result = await _mediator.Send(new GetRelatedProductsQuery(id, count));

        return Ok(result);
    }
}
```

### CartController.cs

```csharp
using ACommerce.Cart;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyEShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;
    private readonly ICurrentUserService _currentUserService;

    public CartController(
        ICartService cartService,
        ICurrentUserService currentUserService)
    {
        _cartService = cartService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// الحصول على سلة التسوق
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart()
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);
        var cart = await _cartService.GetCartAsync(userId);

        return Ok(cart);
    }

    /// <summary>
    /// إضافة منتج للسلة
    /// </summary>
    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem([FromBody] AddToCartRequest request)
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        var result = await _cartService.AddItemAsync(
            userId,
            request.ProductId,
            request.VariantId,
            request.Quantity);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        var cart = await _cartService.GetCartAsync(userId);

        return Ok(cart);
    }

    /// <summary>
    /// تحديث كمية المنتج
    /// </summary>
    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<CartDto>> UpdateItemQuantity(
        Guid itemId,
        [FromBody] UpdateCartItemRequest request)
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        var result = await _cartService.UpdateQuantityAsync(userId, itemId, request.Quantity);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        var cart = await _cartService.GetCartAsync(userId);

        return Ok(cart);
    }

    /// <summary>
    /// حذف منتج من السلة
    /// </summary>
    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartDto>> RemoveItem(Guid itemId)
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        var result = await _cartService.RemoveItemAsync(userId, itemId);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        var cart = await _cartService.GetCartAsync(userId);

        return Ok(cart);
    }

    /// <summary>
    /// تطبيق كود خصم
    /// </summary>
    [HttpPost("coupon")]
    public async Task<ActionResult<CartDto>> ApplyCoupon([FromBody] ApplyCouponRequest request)
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        var result = await _cartService.ApplyCouponAsync(userId, request.Code);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        var cart = await _cartService.GetCartAsync(userId);

        return Ok(cart);
    }

    /// <summary>
    /// إزالة كود الخصم
    /// </summary>
    [HttpDelete("coupon")]
    public async Task<ActionResult<CartDto>> RemoveCoupon()
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        await _cartService.RemoveCouponAsync(userId);

        var cart = await _cartService.GetCartAsync(userId);

        return Ok(cart);
    }

    /// <summary>
    /// تفريغ السلة
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        await _cartService.ClearCartAsync(userId);

        return NoContent();
    }
}

// DTOs
public record AddToCartRequest(Guid ProductId, Guid? VariantId, int Quantity = 1);
public record UpdateCartItemRequest(int Quantity);
public record ApplyCouponRequest(string Code);
```

### OrdersController.cs

```csharp
using ACommerce.Orders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyEShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public OrdersController(
        IMediator mediator,
        ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// الحصول على طلبات المستخدم
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SmartSearchResult<OrderListDto>>> GetMyOrders(
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        var query = new SearchOrdersQuery(
            CustomerId: userId,
            Status: status,
            Page: page,
            PageSize: size);

        var result = await _mediator.Send(query);

        return Ok(result);
    }

    /// <summary>
    /// الحصول على طلب محدد
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDetailDto>> GetById(Guid id)
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        var result = await _mediator.Send(new GetOrderByIdQuery(id));

        if (result == null)
            return NotFound();

        // التأكد من أن الطلب يخص المستخدم الحالي
        if (result.CustomerId != userId && !User.IsInRole("Admin"))
            return Forbid();

        return Ok(result);
    }

    /// <summary>
    /// إنشاء طلب جديد
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderDetailDto>> Create([FromBody] CreateOrderRequest request)
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);
        var userEmail = _currentUserService.GetUserEmail()!;

        var command = new CreateOrderCommand(
            CustomerId: userId,
            CustomerEmail: userEmail,
            CustomerPhone: request.Phone,
            Items: request.Items,
            ShippingAddress: request.ShippingAddress,
            BillingAddress: request.BillingAddress,
            ShippingMethodId: request.ShippingMethodId,
            PaymentMethodId: request.PaymentMethodId,
            CouponCode: request.CouponCode,
            CustomerNote: request.Note);

        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        var order = await _mediator.Send(new GetOrderByIdQuery(result.Value));

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value },
            order);
    }

    /// <summary>
    /// إلغاء طلب
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderRequest request)
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        // Get order first to verify ownership
        var order = await _mediator.Send(new GetOrderByIdQuery(id));

        if (order == null)
            return NotFound();

        if (order.CustomerId != userId && !User.IsInRole("Admin"))
            return Forbid();

        var result = await _mediator.Send(new UpdateOrderStatusCommand(
            OrderId: id,
            NewStatus: OrderStatus.Cancelled,
            CancellationReason: request.Reason));

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    /// <summary>
    /// طلب إرجاع
    /// </summary>
    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> RequestReturn(Guid id, [FromBody] ReturnRequest request)
    {
        var userId = Guid.Parse(_currentUserService.GetUserId()!);

        var order = await _mediator.Send(new GetOrderByIdQuery(id));

        if (order == null)
            return NotFound();

        if (order.CustomerId != userId)
            return Forbid();

        var result = await _mediator.Send(new RequestReturnCommand(
            OrderId: id,
            Reason: request.Reason,
            ItemIds: request.ItemIds));

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }
}

// DTOs
public record CreateOrderRequest(
    string? Phone,
    List<CreateOrderItemDto> Items,
    OrderAddressDto ShippingAddress,
    OrderAddressDto? BillingAddress,
    Guid ShippingMethodId,
    Guid PaymentMethodId,
    string? CouponCode,
    string? Note);

public record CancelOrderRequest(string Reason);
public record ReturnRequest(string Reason, List<Guid>? ItemIds);
```

---

## الخطوة 7: تكوين appsettings.json | Step 7: Configure appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=myeshop;Username=postgres;Password=your_password"
  },
  "Jwt": {
    "SecretKey": "your-super-secret-key-at-least-32-characters-long",
    "Issuer": "https://myeshop.com",
    "Audience": "https://myeshop.com",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Authentication": {
    "LockoutThreshold": 5,
    "LockoutDurationMinutes": 15,
    "RequireEmailConfirmation": false
  },
  "Payments": {
    "Moyasar": {
      "SecretKey": "sk_test_...",
      "PublishableKey": "pk_test_...",
      "WebhookSecret": "whsec_...",
      "IsLive": false
    }
  },
  "Orders": {
    "NumberFormat": {
      "Prefix": "ORD"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

---

## الخطوة 8: إنشاء Migrations | Step 8: Create Migrations

```bash
# إنشاء Migration
dotnet ef migrations add InitialCreate -c AppDbContext -o Data/Migrations

# تطبيق Migration
dotnet ef database update
```

---

## الخطوة 9: تشغيل التطبيق | Step 9: Run Application

```bash
dotnet run

# أو للتطوير
dotnet watch run
```

افتح المتصفح على: https://localhost:5001/swagger

---

## الخطوة 10: Seed البيانات (اختياري) | Step 10: Seed Data (Optional)

### DataSeeder.cs

```csharp
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Categories.AnyAsync())
            return;

        // Seed Categories
        var categories = new List<Category>
        {
            new() { Id = Guid.NewGuid(), Name = "إلكترونيات", Slug = "electronics" },
            new() { Id = Guid.NewGuid(), Name = "ملابس", Slug = "clothing" },
            new() { Id = Guid.NewGuid(), Name = "أحذية", Slug = "shoes" },
        };

        await context.Categories.AddRangeAsync(categories);

        // Seed Products
        var products = new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "هاتف ذكي",
                Slug = "smartphone",
                Sku = "PHONE-001",
                BasePrice = 2999,
                CategoryId = categories[0].Id,
                Status = ProductStatus.Active,
                IsActive = true
            },
            // ... more products
        };

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();
    }
}
```

---

## الخطوات التالية | Next Steps

1. **إضافة اختبارات** - كتابة Unit Tests و Integration Tests
2. **إضافة Caching** - Redis للتخزين المؤقت
3. **إضافة Rate Limiting** - حماية من الطلبات المفرطة
4. **إضافة Health Checks** - مراقبة صحة التطبيق
5. **إضافة Logging** - Serilog أو Seq
6. **إضافة Docker** - حاويات للنشر

---

## المراجع | References

- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
