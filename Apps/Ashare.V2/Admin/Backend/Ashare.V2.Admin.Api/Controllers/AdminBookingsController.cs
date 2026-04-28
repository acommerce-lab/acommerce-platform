using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.V2.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/bookings")]
[Authorize(Policy = "AdminOnly")]
public class AdminBookingsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Booking> _repo;

    public AdminBookingsController(IRepositoryFactory factory) =>
        _repo = factory.CreateRepository<Booking>();

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _repo.GetPagedAsync(
            pageNumber: page, pageSize: pageSize,
            predicate: b => status == null || b.Status == status,
            orderBy: b => b.CreatedAt, ascending: false);

        var rows = result.Items.Select(b => new
        {
            id          = b.Id,
            customerId  = b.CustomerId,
            listingId   = b.ListingId,
            startDate   = b.StartDate,
            endDate     = b.EndDate,
            totalAmount = b.TotalAmount,
            currency    = b.Currency,
            status      = b.Status,
            createdAt   = b.CreatedAt
        });
        return this.OkEnvelope("admin.booking.list", rows);
    }
}
