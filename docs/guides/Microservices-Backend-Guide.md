# دليل إنشاء الباك اند متعدد الخدمات | Microservices Backend Guide

## مقدمة | Introduction

هذا الدليل يشرح كيفية بناء نظام تجارة إلكترونية باستخدام معمارية الخدمات المصغرة (Microservices) مع مكتبات ACommerce. ستتعلم كيفية تقسيم النظام إلى خدمات مستقلة تتواصل عبر رسائل وأحداث.

This guide explains how to build an e-commerce system using Microservices architecture with ACommerce libraries. You'll learn how to split the system into independent services that communicate via messages and events.

---

## البنية المعمارية | Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                   Clients                                        │
│                     (Web App, Mobile App, Admin Dashboard)                       │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ↓
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              API Gateway                                         │
│                        (YARP / Kong / Ocelot)                                   │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
         ┌──────────────────────────────┼──────────────────────────────┐
         │                              │                              │
         ↓                              ↓                              ↓
┌─────────────────┐          ┌─────────────────┐          ┌─────────────────┐
│   Identity      │          │    Catalog      │          │    Orders       │
│   Service       │          │    Service      │          │    Service      │
│                 │          │                 │          │                 │
│ - Authentication│          │ - Products      │          │ - Orders        │
│ - Users         │          │ - Categories    │          │ - Checkout      │
│ - Roles         │          │ - Attributes    │          │ - Returns       │
└────────┬────────┘          └────────┬────────┘          └────────┬────────┘
         │                            │                            │
         │                            │                            │
         └────────────────────────────┼────────────────────────────┘
                                      │
                                      ↓
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            Message Bus (RabbitMQ)                                │
│                                                                                  │
│   ┌───────────────┐    ┌───────────────┐    ┌───────────────┐                  │
│   │ user.created  │    │ product.updated│   │ order.placed  │                  │
│   │ user.updated  │    │ inventory.low  │   │ order.shipped │                  │
│   └───────────────┘    └───────────────┘    └───────────────┘                  │
└─────────────────────────────────────────────────────────────────────────────────┘
         │                            │                            │
         ↓                            ↓                            ↓
┌─────────────────┐          ┌─────────────────┐          ┌─────────────────┐
│   Payments      │          │   Inventory     │          │  Notifications  │
│   Service       │          │   Service       │          │    Service      │
│                 │          │                 │          │                 │
│ - Process Pay   │          │ - Stock Mgmt    │          │ - Email         │
│ - Refunds       │          │ - Reservations  │          │ - SMS           │
│ - Webhooks      │          │ - Alerts        │          │ - Push          │
└─────────────────┘          └─────────────────┘          └─────────────────┘
         │                            │                            │
         ↓                            ↓                            ↓
    ┌─────────┐               ┌─────────┐                  ┌─────────┐
    │PostgreSQL│              │PostgreSQL│                 │ MongoDB │
    └─────────┘               └─────────┘                  └─────────┘
```

---

## المتطلبات | Prerequisites

- .NET 9.0 SDK
- Docker و Docker Compose
- PostgreSQL
- RabbitMQ
- Redis
- Kubernetes (اختياري للنشر)

---

## هيكل المشروع | Project Structure

```
MyEShop.Microservices/
├── src/
│   ├── ApiGateway/
│   │   └── MyEShop.ApiGateway/
│   ├── Services/
│   │   ├── Identity/
│   │   │   ├── MyEShop.Identity.Api/
│   │   │   ├── MyEShop.Identity.Domain/
│   │   │   └── MyEShop.Identity.Infrastructure/
│   │   ├── Catalog/
│   │   │   ├── MyEShop.Catalog.Api/
│   │   │   ├── MyEShop.Catalog.Domain/
│   │   │   └── MyEShop.Catalog.Infrastructure/
│   │   ├── Orders/
│   │   │   ├── MyEShop.Orders.Api/
│   │   │   ├── MyEShop.Orders.Domain/
│   │   │   └── MyEShop.Orders.Infrastructure/
│   │   ├── Payments/
│   │   │   ├── MyEShop.Payments.Api/
│   │   │   └── MyEShop.Payments.Infrastructure/
│   │   ├── Inventory/
│   │   │   ├── MyEShop.Inventory.Api/
│   │   │   └── MyEShop.Inventory.Infrastructure/
│   │   └── Notifications/
│   │       ├── MyEShop.Notifications.Api/
│   │       └── MyEShop.Notifications.Infrastructure/
│   ├── Shared/
│   │   ├── MyEShop.Shared.Contracts/
│   │   ├── MyEShop.Shared.EventBus/
│   │   └── MyEShop.Shared.ServiceDefaults/
│   └── BuildingBlocks/
│       └── MyEShop.EventBus.RabbitMQ/
├── docker/
│   ├── docker-compose.yml
│   ├── docker-compose.override.yml
│   └── .env
├── k8s/
│   └── ... (Kubernetes manifests)
└── MyEShop.Microservices.sln
```

---

## الخطوة 1: الحزم المشتركة | Step 1: Shared Packages

### MyEShop.Shared.Contracts

عقود الأحداث المشتركة بين الخدمات.

```csharp
// Events/UserEvents.cs
namespace MyEShop.Shared.Contracts.Events;

public record UserCreatedEvent(
    Guid UserId,
    string Email,
    string Name,
    DateTime CreatedAt
);

public record UserUpdatedEvent(
    Guid UserId,
    string Email,
    string Name,
    DateTime UpdatedAt
);
```

```csharp
// Events/ProductEvents.cs
namespace MyEShop.Shared.Contracts.Events;

public record ProductCreatedEvent(
    Guid ProductId,
    string Name,
    string Sku,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt
);

public record ProductUpdatedEvent(
    Guid ProductId,
    string Name,
    decimal Price,
    DateTime UpdatedAt
);

public record InventoryLowEvent(
    Guid ProductId,
    string Sku,
    int CurrentStock,
    int ThresholdStock
);
```

```csharp
// Events/OrderEvents.cs
namespace MyEShop.Shared.Contracts.Events;

public record OrderPlacedEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    decimal TotalAmount,
    List<OrderItemInfo> Items,
    DateTime PlacedAt
);

public record OrderItemInfo(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice
);

public record OrderPaidEvent(
    Guid OrderId,
    string OrderNumber,
    decimal Amount,
    string TransactionId,
    DateTime PaidAt
);

public record OrderShippedEvent(
    Guid OrderId,
    string OrderNumber,
    string TrackingNumber,
    string Carrier,
    DateTime ShippedAt
);

public record OrderDeliveredEvent(
    Guid OrderId,
    string OrderNumber,
    DateTime DeliveredAt
);

public record OrderCancelledEvent(
    Guid OrderId,
    string OrderNumber,
    string Reason,
    DateTime CancelledAt
);
```

### MyEShop.Shared.EventBus

واجهة ناقل الأحداث.

```csharp
// IEventBus.cs
namespace MyEShop.Shared.EventBus;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;

    void Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IEventHandler<TEvent>;
}

public interface IEventHandler<in TEvent>
    where TEvent : class
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
```

### MyEShop.EventBus.RabbitMQ

تنفيذ RabbitMQ.

```csharp
// RabbitMQEventBus.cs
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace MyEShop.EventBus.RabbitMQ;

public class RabbitMQEventBus : IEventBus, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMQEventBus> _logger;
    private readonly string _exchangeName;
    private readonly Dictionary<string, Type> _handlers = new();

    public RabbitMQEventBus(
        IConnection connection,
        IServiceProvider serviceProvider,
        ILogger<RabbitMQEventBus> logger,
        string exchangeName = "eshop_event_bus")
    {
        _connection = connection;
        _channel = connection.CreateModel();
        _serviceProvider = serviceProvider;
        _logger = logger;
        _exchangeName = exchangeName;

        // Declare exchange
        _channel.ExchangeDeclare(
            exchange: _exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        var eventName = typeof(TEvent).Name;
        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);

        var properties = _channel.CreateBasicProperties();
        properties.DeliveryMode = 2; // Persistent
        properties.ContentType = "application/json";
        properties.MessageId = Guid.NewGuid().ToString();
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: _exchangeName,
            routingKey: eventName,
            basicProperties: properties,
            body: body);

        _logger.LogInformation(
            "Published event {EventName}: {Message}",
            eventName,
            message);

        await Task.CompletedTask;
    }

    public void Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        var handlerType = typeof(THandler);

        // Create queue for this service
        var queueName = $"{eventName}_{handlerType.Name}";

        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        _channel.QueueBind(
            queue: queueName,
            exchange: _exchangeName,
            routingKey: eventName);

        _handlers[eventName] = handlerType;

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (sender, args) =>
        {
            var message = Encoding.UTF8.GetString(args.Body.ToArray());

            try
            {
                await ProcessEvent(eventName, message);
                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventName}", eventName);
                // Requeue or send to dead letter queue
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(
            queue: queueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation(
            "Subscribed to {EventName} with handler {HandlerName}",
            eventName,
            handlerType.Name);
    }

    private async Task ProcessEvent(string eventName, string message)
    {
        if (!_handlers.TryGetValue(eventName, out var handlerType))
            return;

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService(handlerType);

        var eventType = handlerType.GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
            .GetGenericArguments()[0];

        var @event = JsonSerializer.Deserialize(message, eventType);

        var handleMethod = handlerType.GetMethod("HandleAsync");
        await (Task)handleMethod!.Invoke(handler, new[] { @event, CancellationToken.None })!;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
```

---

## الخطوة 2: خدمة الهوية | Step 2: Identity Service

### MyEShop.Identity.Api/Program.cs

```csharp
using ACommerce.Authentication.JWT;
using MyEShop.Identity.Infrastructure;
using MyEShop.Shared.EventBus;
using MyEShop.EventBus.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

// Add RabbitMQ Event Bus
builder.Services.AddRabbitMQEventBus(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### AuthController.cs مع نشر الأحداث

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IEventBus _eventBus;

    public AuthController(
        IAuthenticationService authService,
        IEventBus eventBus)
    {
        _authService = authService;
        _eventBus = eventBus;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResult>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        if (result.Succeeded)
        {
            // نشر حدث إنشاء المستخدم
            await _eventBus.PublishAsync(new UserCreatedEvent(
                UserId: result.User!.Id,
                Email: result.User.Email,
                Name: result.User.FullName ?? result.User.Email,
                CreatedAt: DateTime.UtcNow));
        }

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
```

---

## الخطوة 3: خدمة الكتالوج | Step 3: Catalog Service

### MyEShop.Catalog.Api/Program.cs

```csharp
using ACommerce.Catalog.Products;
using ACommerce.SharedKernel.CQRS;
using MyEShop.Catalog.Infrastructure;
using MyEShop.EventBus.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddCatalogInfrastructure(builder.Configuration);
builder.Services.AddCqrsWithTransaction(typeof(CreateProductCommand).Assembly);

// Add RabbitMQ
builder.Services.AddRabbitMQEventBus(builder.Configuration);

// Subscribe to events from other services
builder.Services.AddScoped<OrderPlacedEventHandler>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add JWT validation (validates tokens from Identity Service)
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["IdentityService:Url"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false
        };
    });

var app = builder.Build();

// Subscribe to events
var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<OrderPlacedEvent, OrderPlacedEventHandler>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### ProductsController مع نشر الأحداث

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;

    public ProductsController(IMediator mediator, IEventBus eventBus)
    {
        _mediator = mediator;
        _eventBus = eventBus;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProductCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            // نشر حدث إنشاء المنتج
            await _eventBus.PublishAsync(new ProductCreatedEvent(
                ProductId: result.Value,
                Name: command.Name,
                Sku: command.Sku,
                Price: command.BasePrice,
                StockQuantity: command.StockQuantity,
                CreatedAt: DateTime.UtcNow));
        }

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.Error);
    }
}
```

---

## الخطوة 4: خدمة الطلبات | Step 4: Orders Service

### OrdersController مع نشر الأحداث

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateOrderCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            // نشر حدث إنشاء الطلب
            await _eventBus.PublishAsync(new OrderPlacedEvent(
                OrderId: result.Value,
                OrderNumber: result.Data.OrderNumber,
                CustomerId: command.CustomerId,
                TotalAmount: result.Data.Total,
                Items: command.Items.Select(i => new OrderItemInfo(
                    i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
                PlacedAt: DateTime.UtcNow));
        }

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.Error);
    }
}
```

### معالج حدث الدفع

```csharp
public class OrderPaidEventHandler : IEventHandler<OrderPaidEvent>
{
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderPaidEventHandler> _logger;

    public async Task HandleAsync(OrderPaidEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing OrderPaidEvent for order {OrderNumber}",
            @event.OrderNumber);

        // تحديث حالة الطلب
        var result = await _mediator.Send(new UpdateOrderPaymentStatusCommand(
            @event.OrderId,
            PaymentStatus.Paid,
            @event.TransactionId));

        if (result.IsSuccess)
        {
            // بدء تجهيز الطلب
            await _mediator.Send(new StartOrderProcessingCommand(@event.OrderId));
        }
    }
}
```

---

## الخطوة 5: خدمة المخزون | Step 5: Inventory Service

### معالج حدث الطلب

```csharp
public class OrderPlacedEventHandler : IEventHandler<OrderPlacedEvent>
{
    private readonly IInventoryService _inventoryService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderPlacedEventHandler> _logger;

    public async Task HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing OrderPlacedEvent for order {OrderNumber}",
            @event.OrderNumber);

        foreach (var item in @event.Items)
        {
            // حجز المخزون
            var result = await _inventoryService.ReserveStockAsync(
                item.ProductId,
                item.Quantity,
                @event.OrderId,
                cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to reserve stock for product {ProductId}: {Error}",
                    item.ProductId,
                    result.Error);

                // نشر حدث فشل حجز المخزون
                await _eventBus.PublishAsync(new InventoryReservationFailedEvent(
                    @event.OrderId,
                    item.ProductId,
                    result.Error!));
            }

            // التحقق من انخفاض المخزون
            var stock = await _inventoryService.GetStockAsync(item.ProductId, cancellationToken);
            if (stock.CurrentQuantity <= stock.LowStockThreshold)
            {
                await _eventBus.PublishAsync(new InventoryLowEvent(
                    item.ProductId,
                    stock.Sku,
                    stock.CurrentQuantity,
                    stock.LowStockThreshold));
            }
        }
    }
}
```

---

## الخطوة 6: خدمة الإشعارات | Step 6: Notifications Service

### معالجات الأحداث

```csharp
public class OrderShippedNotificationHandler : IEventHandler<OrderShippedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly IPushNotificationService _pushService;
    private readonly IOrderQueryService _orderQuery;
    private readonly ILogger<OrderShippedNotificationHandler> _logger;

    public async Task HandleAsync(OrderShippedEvent @event, CancellationToken cancellationToken)
    {
        var order = await _orderQuery.GetOrderDetailsAsync(@event.OrderId, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", @event.OrderId);
            return;
        }

        // إرسال بريد إلكتروني
        await _emailService.SendAsync(new EmailMessage
        {
            To = order.CustomerEmail,
            Subject = $"تم شحن طلبك #{@event.OrderNumber}",
            Body = $@"
                <h2>تم شحن طلبك!</h2>
                <p>رقم الطلب: {@event.OrderNumber}</p>
                <p>شركة الشحن: {@event.Carrier}</p>
                <p>رقم التتبع: {@event.TrackingNumber}</p>
                <p><a href='https://track.example.com/{@event.TrackingNumber}'>تتبع الشحنة</a></p>
            ",
            IsHtml = true
        }, cancellationToken);

        // إرسال رسالة نصية
        if (!string.IsNullOrEmpty(order.CustomerPhone))
        {
            await _smsService.SendAsync(
                order.CustomerPhone,
                $"تم شحن طلبك #{@event.OrderNumber}. رقم التتبع: {@event.TrackingNumber}",
                cancellationToken);
        }

        // إرسال إشعار Push
        await _pushService.SendAsync(new PushNotification
        {
            UserId = order.CustomerId.ToString(),
            Title = "تم شحن طلبك",
            Body = $"طلبك #{@event.OrderNumber} في الطريق إليك",
            Data = new Dictionary<string, string>
            {
                ["orderId"] = @event.OrderId.ToString(),
                ["trackingNumber"] = @event.TrackingNumber
            }
        }, cancellationToken);
    }
}
```

---

## الخطوة 7: API Gateway | Step 7: API Gateway

### YARP Configuration (appsettings.json)

```json
{
  "ReverseProxy": {
    "Routes": {
      "identity-route": {
        "ClusterId": "identity-cluster",
        "Match": {
          "Path": "/api/auth/{**catch-all}"
        },
        "Transforms": [
          { "PathRemovePrefix": "/api/auth" },
          { "PathPrefix": "/api/auth" }
        ]
      },
      "catalog-route": {
        "ClusterId": "catalog-cluster",
        "Match": {
          "Path": "/api/products/{**catch-all}"
        }
      },
      "catalog-categories-route": {
        "ClusterId": "catalog-cluster",
        "Match": {
          "Path": "/api/categories/{**catch-all}"
        }
      },
      "orders-route": {
        "ClusterId": "orders-cluster",
        "Match": {
          "Path": "/api/orders/{**catch-all}"
        }
      },
      "cart-route": {
        "ClusterId": "orders-cluster",
        "Match": {
          "Path": "/api/cart/{**catch-all}"
        }
      },
      "payments-route": {
        "ClusterId": "payments-cluster",
        "Match": {
          "Path": "/api/payments/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "identity-cluster": {
        "Destinations": {
          "identity-service": {
            "Address": "http://identity-service:8080"
          }
        }
      },
      "catalog-cluster": {
        "Destinations": {
          "catalog-service": {
            "Address": "http://catalog-service:8080"
          }
        }
      },
      "orders-cluster": {
        "Destinations": {
          "orders-service": {
            "Address": "http://orders-service:8080"
          }
        }
      },
      "payments-cluster": {
        "Destinations": {
          "payments-service": {
            "Address": "http://payments-service:8080"
          }
        }
      }
    }
  }
}
```

### ApiGateway/Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
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

app.UseRateLimiter();
app.UseCors("AllowAll");

app.MapReverseProxy();

app.Run();
```

---

## الخطوة 8: Docker Compose | Step 8: Docker Compose

### docker-compose.yml

```yaml
version: '3.8'

services:
  # Infrastructure
  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest

  redis:
    image: redis:7
    ports:
      - "6379:6379"

  # Services
  api-gateway:
    build:
      context: .
      dockerfile: src/ApiGateway/MyEShop.ApiGateway/Dockerfile
    ports:
      - "5000:8080"
    depends_on:
      - identity-service
      - catalog-service
      - orders-service
      - payments-service

  identity-service:
    build:
      context: .
      dockerfile: src/Services/Identity/MyEShop.Identity.Api/Dockerfile
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=identity;Username=postgres;Password=postgres
      - RabbitMQ__Host=rabbitmq
    depends_on:
      - postgres
      - rabbitmq

  catalog-service:
    build:
      context: .
      dockerfile: src/Services/Catalog/MyEShop.Catalog.Api/Dockerfile
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=catalog;Username=postgres;Password=postgres
      - RabbitMQ__Host=rabbitmq
      - IdentityService__Url=http://identity-service:8080
    depends_on:
      - postgres
      - rabbitmq

  orders-service:
    build:
      context: .
      dockerfile: src/Services/Orders/MyEShop.Orders.Api/Dockerfile
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=orders;Username=postgres;Password=postgres
      - RabbitMQ__Host=rabbitmq
      - IdentityService__Url=http://identity-service:8080
    depends_on:
      - postgres
      - rabbitmq

  payments-service:
    build:
      context: .
      dockerfile: src/Services/Payments/MyEShop.Payments.Api/Dockerfile
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=payments;Username=postgres;Password=postgres
      - RabbitMQ__Host=rabbitmq
    depends_on:
      - postgres
      - rabbitmq

  inventory-service:
    build:
      context: .
      dockerfile: src/Services/Inventory/MyEShop.Inventory.Api/Dockerfile
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=inventory;Username=postgres;Password=postgres
      - RabbitMQ__Host=rabbitmq
    depends_on:
      - postgres
      - rabbitmq

  notifications-service:
    build:
      context: .
      dockerfile: src/Services/Notifications/MyEShop.Notifications.Api/Dockerfile
    environment:
      - RabbitMQ__Host=rabbitmq
      - Redis__Host=redis
    depends_on:
      - rabbitmq
      - redis

volumes:
  postgres_data:
```

---

## أفضل الممارسات | Best Practices

### 1. Event Versioning

```csharp
// V1
public record OrderPlacedEventV1(Guid OrderId, decimal Total);

// V2 - backwards compatible
public record OrderPlacedEventV2(
    Guid OrderId,
    decimal Total,
    string? CouponCode = null // optional new field
);
```

### 2. Idempotency

```csharp
public class IdempotentEventHandler<TEvent> : IEventHandler<TEvent>
    where TEvent : class
{
    private readonly IEventHandler<TEvent> _inner;
    private readonly IIdempotencyStore _store;

    public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        var eventId = GetEventId(@event);

        if (await _store.HasProcessedAsync(eventId))
        {
            return; // Already processed
        }

        await _inner.HandleAsync(@event, cancellationToken);
        await _store.MarkProcessedAsync(eventId);
    }
}
```

### 3. Circuit Breaker

```csharp
builder.Services.AddHttpClient<ICatalogService, CatalogServiceClient>()
    .AddPolicyHandler(GetCircuitBreakerPolicy());

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
```

---

## المراجع | References

- [Microservices Pattern by Chris Richardson](https://microservices.io/)
- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [.NET Microservices Architecture](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/)
