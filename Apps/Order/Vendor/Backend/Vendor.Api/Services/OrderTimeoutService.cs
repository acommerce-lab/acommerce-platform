using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Vendor.Api.Entities;
using ACommerce.OrderPlatform.Entities;

namespace Vendor.Api.Services;

/// <summary>
/// Background service that polls for timed-out pending orders every 30 seconds.
/// When an order's TimeoutAt has passed, it auto-rejects via an OpEngine entry
/// (order.timeout) and sends a callback to Order.Api.
/// </summary>
public class OrderTimeoutService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OrderTimeoutService> _logger;

    public OrderTimeoutService(IServiceProvider services, ILogger<OrderTimeoutService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTimeoutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order timeouts");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessTimeoutsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<IRepositoryFactory>()
            .CreateRepository<IncomingOrder>();
        var engine = scope.ServiceProvider.GetRequiredService<OpEngine>();
        var callback = scope.ServiceProvider.GetRequiredService<OrderApiCallback>();

        var pending = await orders.GetAllWithPredicateAsync(
            o => o.Status == IncomingOrderStatus.Pending);

        var now = DateTime.UtcNow;
        foreach (var order in pending.Where(o => o.TimeoutAt <= now))
        {
            _logger.LogInformation("Order {OrderNumber} timed out (timeout was {Timeout})",
                order.OrderNumber, order.TimeoutAt);

            var op = Entry.Create("order.timeout")
                .Describe($"Order {order.OrderNumber} auto-rejected — vendor did not respond within timeout")
                .From($"Vendor:{order.VendorId}", order.Total, ("role", "vendor"), ("currency", order.Currency))
                .To($"Order:{order.OrderApiId}", order.Total, ("role", "order"))
                .Tag("order_number", order.OrderNumber)
                .Tag("timeout_reason", "vendor_no_response")
                .Execute(async ctx =>
                {
                    order.Status = IncomingOrderStatus.TimedOut;
                    order.RespondedAt = DateTime.UtcNow;
                    order.UpdatedAt = DateTime.UtcNow;
                    await orders.UpdateAsync(order, ctx.CancellationToken);
                })
                .Build();

            var result = await engine.ExecuteAsync(op, ct);
            if (result.Success)
            {
                // Notify Order.Api to cancel the customer-side order
                await callback.NotifyStatusAsync(order.OrderApiId, "timeout", ct);
            }
        }
    }
}
