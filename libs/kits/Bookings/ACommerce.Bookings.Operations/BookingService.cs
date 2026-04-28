using ACommerce.Bookings.Operations.Abstractions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Domain.Entities;
using ACommerce.SharedKernel.Repositories.Interfaces;

namespace ACommerce.Bookings.Operations;

/// <summary>Domain-agnostic booking lifecycle service using OperationEngine.</summary>
public class BookingService<T> where T : class, IBaseEntity, IBookingEntity
{
    private readonly IBaseAsyncRepository<T> _bookings;
    private readonly OpEngine _engine;

    public BookingService(IBaseAsyncRepository<T> bookings, OpEngine engine)
    {
        _bookings = bookings ?? throw new ArgumentNullException(nameof(bookings));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public async Task<OperationResult> ConfirmAsync(T booking, CancellationToken ct = default)
    {
        var op = Entry.Create("booking.confirm")
            .From($"Owner:{booking.OwnerId}", 1, ("role", "owner"))
            .To($"Booking:{booking.Id}", 1, ("role", "confirmed"))
            .Tag("resource_id", booking.ResourceId.ToString())
            .Tag("customer_id", booking.CustomerId.ToString())
            .Execute(async ctx =>
            {
                booking.Status = BookingStatuses.Confirmed;
                booking.UpdatedAt = DateTime.UtcNow;
                booking.OperationId = ctx.Operation.Id;
                await _bookings.UpdateAsync(booking, ctx.CancellationToken);
            })
            .Build();

        return await _engine.ExecuteAsync(op, ct);
    }

    public async Task<OperationResult> RejectAsync(T booking, CancellationToken ct = default)
    {
        var op = Entry.Create("booking.reject")
            .From($"Owner:{booking.OwnerId}", 1, ("role", "owner"))
            .To($"Booking:{booking.Id}", 1, ("role", "rejected"))
            .Tag("resource_id", booking.ResourceId.ToString())
            .Tag("customer_id", booking.CustomerId.ToString())
            .Execute(async ctx =>
            {
                booking.Status = BookingStatuses.Cancelled;
                booking.UpdatedAt = DateTime.UtcNow;
                booking.OperationId = ctx.Operation.Id;
                await _bookings.UpdateAsync(booking, ctx.CancellationToken);
            })
            .Build();

        return await _engine.ExecuteAsync(op, ct);
    }

    public async Task<OperationResult> CancelAsync(T booking, CancellationToken ct = default)
    {
        var op = Entry.Create("booking.cancel")
            .From($"Customer:{booking.CustomerId}", 1, ("role", "customer"))
            .To($"Booking:{booking.Id}", 1, ("role", "cancelled"))
            .Tag("resource_id", booking.ResourceId.ToString())
            .Tag("owner_id", booking.OwnerId.ToString())
            .Execute(async ctx =>
            {
                booking.Status = BookingStatuses.Cancelled;
                booking.UpdatedAt = DateTime.UtcNow;
                booking.OperationId = ctx.Operation.Id;
                await _bookings.UpdateAsync(booking, ctx.CancellationToken);
            })
            .Build();

        return await _engine.ExecuteAsync(op, ct);
    }

    public async Task<OperationResult> CompleteAsync(T booking, CancellationToken ct = default)
    {
        var op = Entry.Create("booking.complete")
            .From($"Owner:{booking.OwnerId}", 1, ("role", "owner"))
            .To($"Booking:{booking.Id}", 1, ("role", "completed"))
            .Tag("resource_id", booking.ResourceId.ToString())
            .Tag("customer_id", booking.CustomerId.ToString())
            .Execute(async ctx =>
            {
                booking.Status = BookingStatuses.Completed;
                booking.UpdatedAt = DateTime.UtcNow;
                booking.OperationId = ctx.Operation.Id;
                await _bookings.UpdateAsync(booking, ctx.CancellationToken);
            })
            .Build();

        return await _engine.ExecuteAsync(op, ct);
    }
}
