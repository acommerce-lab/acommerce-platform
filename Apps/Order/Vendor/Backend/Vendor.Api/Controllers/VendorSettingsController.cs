using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Vendor.Api.Entities;
using ACommerce.OrderPlatform.Entities;

namespace Vendor.Api.Controllers;

[ApiController]
[Route("api/vendor-settings")]
public class VendorSettingsController : ControllerBase
{
    private readonly IBaseAsyncRepository<VendorSettings> _settings;
    private readonly IBaseAsyncRepository<WorkSchedule> _schedules;
    private readonly OpEngine _engine;

    public VendorSettingsController(IRepositoryFactory factory, OpEngine engine)
    {
        _settings = factory.CreateRepository<VendorSettings>();
        _schedules = factory.CreateRepository<WorkSchedule>();
        _engine = engine;
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

        var op = Entry.Create("vendor-settings.update")
            .Describe($"Vendor:{vendorId} updates order acceptance settings")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"VendorSettings:{s.Id}", 1, ("role", "settings"))
            .Tag("vendor_id", vendorId.ToString())
            .Tag("accepting_orders", (req.AcceptingOrders ?? s.AcceptingOrders).ToString())
            .Execute(async ctx =>
            {
                if (req.AcceptingOrders.HasValue) s.AcceptingOrders = req.AcceptingOrders.Value;
                if (req.MaxConcurrentPending.HasValue) s.MaxConcurrentPending = req.MaxConcurrentPending.Value;
                if (req.OrderTimeoutMinutes.HasValue) s.OrderTimeoutMinutes = Math.Max(1, req.OrderTimeoutMinutes.Value);
                s.UpdatedAt = DateTime.UtcNow;
                await _settings.UpdateAsync(s, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("settings_update_failed", result.ErrorMessage);
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

        var op = Entry.Create("vendor-schedule.update")
            .Describe($"Vendor:{vendorId} updates {(DayOfWeek)req.DayOfWeek} schedule")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"WorkSchedule:{day.Id}", 1, ("role", "schedule"))
            .Tag("vendor_id", vendorId.ToString())
            .Tag("day_of_week", req.DayOfWeek.ToString())
            .Execute(async ctx =>
            {
                if (req.OpenTime != null) day.OpenTime = req.OpenTime;
                if (req.CloseTime != null) day.CloseTime = req.CloseTime;
                if (req.IsOff.HasValue) day.IsOff = req.IsOff.Value;
                day.UpdatedAt = DateTime.UtcNow;
                await _schedules.UpdateAsync(day, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("schedule_update_failed", result.ErrorMessage);
        return this.OkEnvelope("vendor-schedule.update", day);
    }
}
