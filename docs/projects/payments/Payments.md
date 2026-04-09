# ACommerce.Payments

## نظرة عامة | Overview

مكتبات `ACommerce.Payments.*` توفر نظام مدفوعات متكامل وقابل للتوسع مع دعم مزودين متعددين. تتيح البنية المعمارية إضافة مزودي دفع جدد بسهولة دون تغيير الكود الأساسي.

The `ACommerce.Payments.*` libraries provide a complete and extensible payment system with multi-provider support. The architecture allows easy addition of new payment providers without changing core code.

---

## البنية المعمارية | Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Payment Service Layer                        │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │           IPaymentService (Abstractions)                  │  │
│  │    InitiatePayment | Capture | Refund | GetStatus        │  │
│  └──────────────────────────────────────────────────────────┘  │
│                              ↑                                   │
│                    ┌─────────┴─────────┐                        │
│                    │ PaymentProviderFactory                     │
│                    └─────────┬─────────┘                        │
│         ┌────────────────────┼────────────────────┐             │
│         │                    │                    │             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │   Moyasar    │    │    Stripe    │    │    PayPal    │      │
│  └──────────────┘    └──────────────┘    └──────────────┘      │
│         │                    │                    │             │
│         ↓                    ↓                    ↓             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │  Moyasar API │    │  Stripe API  │    │  PayPal API  │      │
│  └──────────────┘    └──────────────┘    └──────────────┘      │
└─────────────────────────────────────────────────────────────────┘
```

---

# ACommerce.Payments.Abstractions

## المسار | Path
`Payments/ACommerce.Payments.Abstractions`

## الواجهات الرئيسية | Core Interfaces

### IPaymentService

```csharp
public interface IPaymentService
{
    /// <summary>
    /// اسم المزود
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// بدء عملية دفع جديدة
    /// </summary>
    Task<PaymentResult> InitiatePaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// التقاط الدفع (للمدفوعات المعتمدة مسبقاً)
    /// </summary>
    Task<PaymentResult> CapturePaymentAsync(
        string paymentId,
        decimal? amount = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// استرداد المبلغ
    /// </summary>
    Task<RefundResult> RefundAsync(
        string paymentId,
        decimal? amount = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// إلغاء الدفع (للمدفوعات المعلقة)
    /// </summary>
    Task<Result> VoidPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// الحصول على حالة الدفع
    /// </summary>
    Task<PaymentStatusResult> GetStatusAsync(
        string paymentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// معالجة Webhook من المزود
    /// </summary>
    Task<WebhookResult> ProcessWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default);
}
```

### IPaymentProviderFactory

```csharp
public interface IPaymentProviderFactory
{
    IPaymentService GetProvider(string providerName);
    IEnumerable<string> GetAvailableProviders();
    bool IsProviderAvailable(string providerName);
}
```

---

## نماذج البيانات | Data Models

### PaymentRequest

```csharp
public class PaymentRequest
{
    /// <summary>
    /// معرف فريد للطلب
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// رقم الطلب المقروء
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// المبلغ
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// كود العملة (SAR, USD, etc.)
    /// </summary>
    public string Currency { get; set; } = "SAR";

    /// <summary>
    /// وصف الدفعة
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// معلومات العميل
    /// </summary>
    public PaymentCustomer Customer { get; set; } = new();

    /// <summary>
    /// طريقة الدفع المحددة (اختياري)
    /// </summary>
    public PaymentMethod? Method { get; set; }

    /// <summary>
    /// رابط العودة بعد الدفع الناجح
    /// </summary>
    public string? SuccessUrl { get; set; }

    /// <summary>
    /// رابط العودة بعد فشل الدفع
    /// </summary>
    public string? FailureUrl { get; set; }

    /// <summary>
    /// رابط Webhook
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// بيانات إضافية
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// التقاط تلقائي أم تفويض فقط
    /// </summary>
    public bool AutoCapture { get; set; } = true;
}
```

### PaymentCustomer

```csharp
public class PaymentCustomer
{
    public string? Id { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Name { get; set; }
    public PaymentAddress? Address { get; set; }
}

public class PaymentAddress
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}
```

### PaymentResult

```csharp
public class PaymentResult
{
    public bool IsSuccess { get; set; }
    public string? PaymentId { get; set; }
    public string? TransactionId { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// رابط إعادة التوجيه (للمدفوعات التي تتطلب واجهة)
    /// </summary>
    public string? RedirectUrl { get; set; }

    /// <summary>
    /// هل تتطلب إعادة توجيه؟
    /// </summary>
    public bool RequiresRedirect => !string.IsNullOrEmpty(RedirectUrl);

    /// <summary>
    /// رسالة الخطأ (في حالة الفشل)
    /// </summary>
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }

    /// <summary>
    /// البيانات الخام من المزود
    /// </summary>
    public Dictionary<string, object>? ProviderData { get; set; }

    public static PaymentResult Success(
        string paymentId,
        string transactionId,
        decimal amount,
        string currency,
        string? redirectUrl = null) => new()
    {
        IsSuccess = true,
        PaymentId = paymentId,
        TransactionId = transactionId,
        Status = PaymentStatus.Pending,
        Amount = amount,
        Currency = currency,
        RedirectUrl = redirectUrl
    };

    public static PaymentResult Failure(string errorMessage, string? errorCode = null) => new()
    {
        IsSuccess = false,
        Status = PaymentStatus.Failed,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };
}
```

### RefundResult

```csharp
public class RefundResult
{
    public bool IsSuccess { get; set; }
    public string? RefundId { get; set; }
    public decimal AmountRefunded { get; set; }
    public RefundStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}

public enum RefundStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3
}
```

### PaymentStatus

```csharp
public enum PaymentStatus
{
    /// <summary>
    /// في انتظار إجراء من العميل
    /// </summary>
    Pending = 0,

    /// <summary>
    /// قيد المعالجة
    /// </summary>
    Processing = 1,

    /// <summary>
    /// تم التفويض (في انتظار التقاط)
    /// </summary>
    Authorized = 2,

    /// <summary>
    /// تم الدفع بنجاح
    /// </summary>
    Succeeded = 3,

    /// <summary>
    /// فشل الدفع
    /// </summary>
    Failed = 4,

    /// <summary>
    /// ملغي
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// مسترد بالكامل
    /// </summary>
    Refunded = 6,

    /// <summary>
    /// مسترد جزئياً
    /// </summary>
    PartiallyRefunded = 7,

    /// <summary>
    /// متنازع عليه
    /// </summary>
    Disputed = 8
}
```

### PaymentMethod

```csharp
public enum PaymentMethod
{
    CreditCard = 0,
    DebitCard = 1,
    Mada = 2,
    ApplePay = 3,
    STCPay = 4,
    BankTransfer = 5,
    CashOnDelivery = 6
}
```

---

# ACommerce.Payments.Moyasar

## نظرة عامة | Overview

تنفيذ مزود الدفع Moyasar - بوابة دفع سعودية تدعم بطاقات الائتمان ومدى وApple Pay وSTC Pay.

**المسار | Path:** `Payments/ACommerce.Payments.Moyasar`

---

## MoyasarPaymentService

```csharp
public class MoyasarPaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly MoyasarOptions _options;
    private readonly ILogger<MoyasarPaymentService> _logger;

    public string ProviderName => "Moyasar";

    public MoyasarPaymentService(
        HttpClient httpClient,
        IOptions<MoyasarOptions> options,
        ILogger<MoyasarPaymentService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Configure HTTP client
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_options.SecretKey}:"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<PaymentResult> InitiatePaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var moyasarRequest = new
            {
                amount = (int)(request.Amount * 100), // Moyasar uses halalas
                currency = request.Currency,
                description = request.Description ?? $"Order {request.OrderNumber}",
                callback_url = request.SuccessUrl,
                source = new
                {
                    type = GetSourceType(request.Method),
                    // Additional source configuration based on method
                },
                metadata = new
                {
                    order_id = request.OrderId,
                    order_number = request.OrderNumber
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                "v1/payments",
                moyasarRequest,
                cancellationToken);

            var content = await response.Content.ReadFromJsonAsync<MoyasarPaymentResponse>(
                cancellationToken: cancellationToken);

            if (response.IsSuccessStatusCode && content != null)
            {
                _logger.LogInformation(
                    "Moyasar payment initiated: {PaymentId}",
                    content.Id);

                return PaymentResult.Success(
                    content.Id,
                    content.Id,
                    request.Amount,
                    request.Currency,
                    content.Source?.TransactionUrl);
            }

            _logger.LogWarning(
                "Moyasar payment failed: {Error}",
                content?.Message ?? "Unknown error");

            return PaymentResult.Failure(
                content?.Message ?? "فشل في بدء عملية الدفع",
                content?.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Moyasar payment");
            return PaymentResult.Failure("حدث خطأ أثناء معالجة الدفع");
        }
    }

    public async Task<PaymentResult> CapturePaymentAsync(
        string paymentId,
        decimal? amount = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var captureRequest = amount.HasValue
                ? new { amount = (int)(amount.Value * 100) }
                : null;

            var response = await _httpClient.PostAsJsonAsync(
                $"v1/payments/{paymentId}/capture",
                captureRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<MoyasarPaymentResponse>(
                    cancellationToken: cancellationToken);

                return PaymentResult.Success(
                    content!.Id,
                    content.Id,
                    content.Amount / 100m,
                    content.Currency);
            }

            return PaymentResult.Failure("فشل في التقاط الدفع");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing Moyasar payment");
            return PaymentResult.Failure("حدث خطأ أثناء التقاط الدفع");
        }
    }

    public async Task<RefundResult> RefundAsync(
        string paymentId,
        decimal? amount = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var refundRequest = new
            {
                amount = amount.HasValue ? (int)(amount.Value * 100) : (int?)null,
                reason
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"v1/payments/{paymentId}/refund",
                refundRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<MoyasarRefundResponse>(
                    cancellationToken: cancellationToken);

                return new RefundResult
                {
                    IsSuccess = true,
                    RefundId = content!.Id,
                    AmountRefunded = content.Amount / 100m,
                    Status = RefundStatus.Succeeded
                };
            }

            return new RefundResult
            {
                IsSuccess = false,
                Status = RefundStatus.Failed,
                ErrorMessage = "فشل في استرداد المبلغ"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding Moyasar payment");
            return new RefundResult
            {
                IsSuccess = false,
                Status = RefundStatus.Failed,
                ErrorMessage = "حدث خطأ أثناء الاسترداد"
            };
        }
    }

    public async Task<PaymentStatusResult> GetStatusAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"v1/payments/{paymentId}",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<MoyasarPaymentResponse>(
                    cancellationToken: cancellationToken);

                return new PaymentStatusResult
                {
                    IsSuccess = true,
                    PaymentId = content!.Id,
                    Status = MapMoyasarStatus(content.Status),
                    Amount = content.Amount / 100m,
                    Currency = content.Currency
                };
            }

            return new PaymentStatusResult
            {
                IsSuccess = false,
                ErrorMessage = "فشل في الحصول على حالة الدفع"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Moyasar payment status");
            return new PaymentStatusResult
            {
                IsSuccess = false,
                ErrorMessage = "حدث خطأ"
            };
        }
    }

    public async Task<WebhookResult> ProcessWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify webhook signature
            if (!VerifyWebhookSignature(payload, signature))
            {
                return WebhookResult.Invalid("Invalid signature");
            }

            var webhookData = JsonSerializer.Deserialize<MoyasarWebhookPayload>(payload);

            if (webhookData == null)
            {
                return WebhookResult.Invalid("Invalid payload");
            }

            _logger.LogInformation(
                "Moyasar webhook received: {Type} for {PaymentId}",
                webhookData.Type,
                webhookData.Data?.Id);

            return new WebhookResult
            {
                IsValid = true,
                EventType = webhookData.Type,
                PaymentId = webhookData.Data?.Id,
                Status = MapMoyasarStatus(webhookData.Data?.Status ?? ""),
                Metadata = webhookData.Data?.Metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Moyasar webhook");
            return WebhookResult.Invalid("Error processing webhook");
        }
    }

    private static string GetSourceType(PaymentMethod? method) => method switch
    {
        PaymentMethod.CreditCard => "creditcard",
        PaymentMethod.Mada => "mada",
        PaymentMethod.ApplePay => "applepay",
        PaymentMethod.STCPay => "stcpay",
        _ => "creditcard"
    };

    private static PaymentStatus MapMoyasarStatus(string status) => status.ToLower() switch
    {
        "initiated" => PaymentStatus.Pending,
        "authorized" => PaymentStatus.Authorized,
        "paid" => PaymentStatus.Succeeded,
        "failed" => PaymentStatus.Failed,
        "refunded" => PaymentStatus.Refunded,
        "voided" => PaymentStatus.Cancelled,
        _ => PaymentStatus.Pending
    };

    private bool VerifyWebhookSignature(string payload, string signature)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToHexString(computedHash).ToLower();
        return computedSignature == signature.ToLower();
    }
}
```

---

## MoyasarOptions

```csharp
public class MoyasarOptions
{
    public const string SectionName = "Payments:Moyasar";

    /// <summary>
    /// المفتاح السري (Secret Key)
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// المفتاح العام (Publishable Key)
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// مفتاح التحقق من Webhook
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// البيئة (Test/Live)
    /// </summary>
    public bool IsLive { get; set; }

    /// <summary>
    /// رابط API
    /// </summary>
    public string BaseUrl => IsLive
        ? "https://api.moyasar.com/"
        : "https://api.moyasar.com/"; // Same URL, different keys
}
```

---

## تسجيل الخدمات | Service Registration

```csharp
public static class PaymentsServiceCollectionExtensions
{
    public static IServiceCollection AddMoyasarPayments(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MoyasarOptions>(
            configuration.GetSection(MoyasarOptions.SectionName));

        var options = configuration
            .GetSection(MoyasarOptions.SectionName)
            .Get<MoyasarOptions>();

        services.AddHttpClient<MoyasarPaymentService>(client =>
        {
            client.BaseAddress = new Uri(options!.BaseUrl);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddScoped<IPaymentService, MoyasarPaymentService>();

        return services;
    }

    public static IServiceCollection AddPaymentProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add Moyasar
        services.AddMoyasarPayments(configuration);

        // Add more providers as needed
        // services.AddStripePayments(configuration);
        // services.AddPayPalPayments(configuration);

        // Register factory
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();

        return services;
    }
}
```

---

## استخدام في Controller | Controller Usage

```csharp
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentProviderFactory _paymentFactory;
    private readonly IMediator _mediator;

    public PaymentsController(
        IPaymentProviderFactory paymentFactory,
        IMediator mediator)
    {
        _paymentFactory = paymentFactory;
        _mediator = mediator;
    }

    [HttpPost("initiate")]
    public async Task<ActionResult<PaymentResult>> InitiatePayment(
        [FromBody] InitiatePaymentDto dto)
    {
        var provider = _paymentFactory.GetProvider(dto.ProviderName);

        var result = await provider.InitiatePaymentAsync(new PaymentRequest
        {
            OrderId = dto.OrderId,
            OrderNumber = dto.OrderNumber,
            Amount = dto.Amount,
            Currency = dto.Currency,
            Customer = new PaymentCustomer
            {
                Email = dto.CustomerEmail,
                Phone = dto.CustomerPhone,
                Name = dto.CustomerName
            },
            SuccessUrl = dto.SuccessUrl,
            FailureUrl = dto.FailureUrl
        });

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("webhook/moyasar")]
    public async Task<IActionResult> MoyasarWebhook()
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["X-Moyasar-Signature"].FirstOrDefault();

        var provider = _paymentFactory.GetProvider("Moyasar");
        var result = await provider.ProcessWebhookAsync(payload, signature ?? "");

        if (!result.IsValid)
            return BadRequest();

        // Update order status based on webhook
        await _mediator.Send(new UpdateOrderPaymentStatusCommand(
            result.PaymentId!,
            result.Status));

        return Ok();
    }
}
```

---

## تكوين appsettings.json | Configuration

```json
{
  "Payments": {
    "Moyasar": {
      "SecretKey": "sk_test_...",
      "PublishableKey": "pk_test_...",
      "WebhookSecret": "whsec_...",
      "IsLive": false
    }
  }
}
```

---

## المراجع | References

- [Moyasar Documentation](https://docs.moyasar.com/)
- [PCI DSS Compliance](https://www.pcisecuritystandards.org/)
- [Payment Gateway Best Practices](https://stripe.com/docs/security)
