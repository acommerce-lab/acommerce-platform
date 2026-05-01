using ACommerce.Kits.Reports.Domain;
using ACommerce.Kits.Reports.Operations;
using Ejar.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Stores;

/// <summary>
/// مخزن البلاغات — جدول <c>Reports</c> في <see cref="EjarDbContext"/>.
/// نحيف عمداً: لا منطق دومين هنا، فقط CRUD مع per-user scoping على
/// <c>ListMineAsync</c>.
/// </summary>
public sealed class EjarReportStore : IReportStore
{
    private readonly EjarDbContext _db;
    public EjarReportStore(EjarDbContext db) => _db = db;

    public Task<IReport> SubmitAsync(
        string reporterId, string entityType, string entityId,
        string reason, string? body, CancellationToken ct)
    {
        if (!Guid.TryParse(reporterId, out var rid))
            throw new InvalidOperationException("invalid_reporter_id");
        var r = new ReportEntity
        {
            Id          = Guid.NewGuid(),
            CreatedAt   = DateTime.UtcNow,
            ReporterId  = rid,
            EntityType  = entityType,
            EntityId    = entityId,
            Reason      = reason,
            Body        = body,
            Status      = "open",
        };
        _db.Reports.Add(r);
        // (F6) لا SaveChanges — ReportsController.Submit يضع .SaveAtEnd().
        return Task.FromResult<IReport>(r);
    }

    public async Task<IReadOnlyList<IReport>> ListMineAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return Array.Empty<IReport>();
        var rows = await _db.Reports.AsNoTracking()
            .Where(r => r.ReporterId == uid)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return rows.Cast<IReport>().ToList();
    }

    public async Task<IReadOnlyList<IReport>> ListAllAsync(string? status, CancellationToken ct)
    {
        var q = _db.Reports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(r => r.Status == status);
        var rows = await q.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return rows.Cast<IReport>().ToList();
    }

    public async Task<bool> SetStatusAsync(string reportId, string newStatus, CancellationToken ct)
    {
        if (!Guid.TryParse(reportId, out var rid)) return false;
        var r = await _db.Reports.FirstOrDefaultAsync(x => x.Id == rid, ct);
        if (r is null) return false;
        r.Status    = newStatus;
        r.UpdatedAt = DateTime.UtcNow;
        // (F6) tracked mutation only. ReportsController.SetStatus يحفظ.
        return true;
    }
}
