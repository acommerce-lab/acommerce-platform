using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Vendor.Api.Entities;

namespace Vendor.Api.Controllers;

[ApiController]
[Route("api/vendor-settings")]
public class VendorSettingsController : ControllerBase
{
    private readonly IBaseAsyncRepository<VendorSettings> _settings;
    private readonly IBaseAsyncRepository<WorkSchedule> _schedules;

    public VendorSettingsController(IRepositoryFactory factory)
    {
        _settings = factory.CreateRepository<VendorSettings>();
        _schedules = factory.CreateRepository<WorkSchedule>();
    }

    [HttpGet("{vendorId:guid}")]
    public async Task<IActionResult> GetSettings(Guid vendorId, CancellationToken ct)
    {
        var s = (await _settings.GetAllWithPredicateAsync(x => x.VendorId == vendorId)).FirstOrDefault();
        if (s == null) return this.NotFoundEnvelope("settings_not_found");
        return this.OkEnvelope("vendor-settings.get", s);
    }

    public record UpdateSettingsRequest(bool? AcceptingOrders, int? MaxConcurrentPending, int? OrderTimeoutMinutes);

    [HttpPut("{vendorId:guid}")]
    public async Task<IActionResult> UpdateSettings(Guid vendorId, [FromBody] UpdateSettingsRequest req, CancellationToken ct)
    {
        var s = (await _settings.GetAllWithPredicateAsync(x => x.VendorId == vendorId)).FirstOrDefault();
        if (s == null) return this.NotFoundEnvelope("settings_not_found");
        if (req.AcceptingOrders.HasValue) s.AcceptingOrders = req.AcceptingOrders.Value;
        if (req.MaxConcurrentPending.HasValue) s.MaxConcurrentPending = req.MaxConcurrentPending.Value;
        if (req.OrderTimeoutMinutes.HasValue) s.OrderTimeoutMinutes = Math.Max(1, req.OrderTimeoutMinutes.Value);
        s.UpdatedAt = DateTime.UtcNow;
        await _settings.UpdateAsync(s, ct);
        return this.OkEnvelope("vendor-settings.update", s);
    }

    [HttpGet("{vendorId:guid}/schedule")]
    public async Task<IActionResult> GetSchedule(Guid vendorId, CancellationToken ct)
    {
        var days = await _schedules.GetAllWithPredicateAsync(x => x.VendorId == vendorId);
        return this.OkEnvelope("vendor-schedule.get", days.OrderBy(d => d.DayOfWeek));
    }

    public record UpdateDayRequest(int DayOfWeek, string? OpenTime, string? CloseTime, bool? IsOff);

    [HttpPut("{vendorId:guid}/schedule")]
    public async Task<IActionResult> UpdateSchedule(Guid vendorId, [FromBody] UpdateDayRequest req, CancellationToken ct)
    {
        var day = (await _schedules.GetAllWithPredicateAsync(
            x => x.VendorId == vendorId && x.DayOfWeek == (DayOfWeek)req.DayOfWeek)).FirstOrDefault();
        if (day == null) return this.NotFoundEnvelope("schedule_day_not_found");
        if (req.OpenTime != null) day.OpenTime = req.OpenTime;
        if (req.CloseTime != null) day.CloseTime = req.CloseTime;
        if (req.IsOff.HasValue) day.IsOff = req.IsOff.Value;
        day.UpdatedAt = DateTime.UtcNow;
        await _schedules.UpdateAsync(day, ct);
        return this.OkEnvelope("vendor-schedule.update", day);
    }
}
