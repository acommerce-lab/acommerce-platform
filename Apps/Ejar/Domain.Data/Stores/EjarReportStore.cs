using ACommerce.Kits.Reports.Domain;
using ACommerce.Kits.Reports.Operations;
using ACommerce.SharedKernel.Infrastructure.EFCore.Stores;
using Ejar.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Stores;

/// <summary>
/// مخزن البلاغات — جدول <c>Reports</c> في <see cref="EjarDbContext"/>.
/// نحيف عمداً: لا منطق دومين هنا، فقط CRUD مع per-user scoping على
/// <c>ListMineAsync</c>. يَرِث <see cref="EfOwnedStoreBase{TEntity}"/>
/// لِـ <c>SetStatusAsync</c> الَّتي صارَت سَطراً واحِداً.
/// </summary>
public sealed class EjarReportStore : EfOwnedStoreBase<ReportEntity>, IReportStore
{
    private readonly EjarDbContext _db;
    public EjarReportStore(EjarDbContext db) : base(db) => _db = db;

    public async Task<IReport> SubmitAsync(
        string reporterId, string entityType, string entityId,
        string reason, string? body, CancellationToken ct)
    {
        // مسار قديم — يبني POCO ثمّ يُمرّره للمسار الجديد AddNoSaveAsync.
        var poco = new InMemoryReport(
            Id:         Guid.NewGuid().ToString(),
            ReporterId: reporterId,
            EntityType: entityType,
            EntityId:   entityId,
            Reason:     reason,
            Body:       body,
            Status:     "open",
            CreatedAt:  DateTime.UtcNow);
        await AddNoSaveAsync(poco, ct);
        return poco;
    }

    public Task AddNoSaveAsync(IReport report, CancellationToken ct)
    {
        if (!Guid.TryParse(report.ReporterId, out var rid))
            throw new InvalidOperationException("invalid_reporter_id");
        var rowId = Guid.TryParse(report.Id, out var mid) ? mid : Guid.NewGuid();
        var entity = new ReportEntity
        {
            Id          = rowId,
            CreatedAt   = report.CreatedAt,
            ReporterId  = rid,
            EntityType  = report.EntityType,
            EntityId    = report.EntityId,
            Reason      = report.Reason,
            Body        = report.Body,
            Status      = report.Status,
        };
        _db.Reports.Add(entity);
        // (F6) لا SaveChanges — ReportsController.Submit يضع .SaveAtEnd().
        return Task.CompletedTask;
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

    public Task<bool> SetStatusAsync(string reportId, string newStatus, CancellationToken ct) =>
        ApplyPatchNoSaveAsync(reportId, newStatus, (r, s) => r.Status = s, ct);
}
