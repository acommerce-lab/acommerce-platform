using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/bookings")]
[Authorize(Policy = "AdminOnly")]
public class AdminBookingsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Booking> _repo;

    public AdminBookingsController(IRepositoryFactory factory)
    {
        _repo = factory.CreateRepository<Booking>();
    }

    /// <summary>
    /// GET /api/admin/bookings?status=&amp;page=1&amp;pageSize=20
    /// قائمة الحجوزات مع فلترة وترقيم.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        BookingStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var s))
            parsedStatus = s;

        // Frontend expects a flat list — return `.Items` directly.
        var result = await _repo.GetPagedAsync(
            pageNumber: page,
            pageSize: pageSize,
            predicate: b => parsedStatus == null || b.Status == parsedStatus,
            orderBy: b => b.CreatedAt,
            ascending: false);

        var rows = result.Items.Select(b => new
        {
            id            = b.Id,
            customerName  = (string?)null,
            customerPhone = (string?)null,
            totalPrice    = b.TotalPrice,
            currency      = b.Currency,
            status        = (int)b.Status,
            startDate     = b.StartDate,
            endDate       = b.EndDate,
            notes         = b.Notes
        }).ToList();

        return this.OkEnvelope("admin.booking.list", rows);
    }

    /// <summary>
    /// GET /api/admin/bookings/{id}
    /// تفاصيل حجز.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var booking = await _repo.GetByIdAsync(id, ct);
        if (booking == null) return this.NotFoundEnvelope("booking_not_found");
        return this.OkEnvelope("admin.booking.get", booking);
    }
}
