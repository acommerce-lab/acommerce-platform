using ACommerce.Kits.Subscriptions.Backend;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Stores;

/// <summary>
/// مخزن الاشتراكات الفعّالة. يلائم
/// <see cref="ACommerce.Kits.Subscriptions.Backend.ISubscriptionStore"/>
/// عبر تحويل <see cref="SubscriptionEntity"/> إلى <see cref="SubscriptionView"/>.
/// </summary>
public sealed class EjarSubscriptionStore : ISubscriptionStore
{
    private readonly EjarDbContext _db;
    public EjarSubscriptionStore(EjarDbContext db) => _db = db;

    public async Task<SubscriptionView?> GetActiveAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return null;
        var s = await _db.Subscriptions.AsNoTracking()
            .Where(x => x.UserId == uid && x.Status == "active")
            .OrderByDescending(x => x.EndDate)
            .FirstOrDefaultAsync(ct);
        return s is null ? null : ToView(s);
    }

    public async Task<SubscriptionView> ActivateAsync(string userId, string planId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid))
            throw new InvalidOperationException("invalid_user_id");
        if (!Guid.TryParse(planId, out var pid))
            throw new InvalidOperationException("invalid_plan_id");

        var plan = await _db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct)
                   ?? throw new InvalidOperationException("plan_not_found");

        // أنهِ الـ active السابقة (واحد نشط لكلّ مستخدم في كلّ وقت).
        var priors = await _db.Subscriptions
            .Where(s => s.UserId == uid && s.Status == "active")
            .ToListAsync(ct);
        foreach (var p in priors) { p.Status = "expired"; p.UpdatedAt = DateTime.UtcNow; }

        var sub = new SubscriptionEntity
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTime.UtcNow,
            UserId           = uid,
            PlanId           = plan.Id,
            PlanName         = plan.Label,
            Status           = "active",
            StartDate        = DateTime.UtcNow,
            EndDate          = DateTime.UtcNow.AddMonths(1),
            ListingsLimit    = plan.MaxActiveListings,
            FeaturedLimit    = plan.MaxFeaturedListings,
            ImagesPerListing = plan.MaxImagesPerListing,
        };
        _db.Subscriptions.Add(sub);

        // فاتورة mock — تثبت الترقية في سجلّ الفواتير.
        _db.Invoices.Add(new InvoiceEntity
        {
            Id        = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId    = uid,
            PlanId    = plan.Id,
            Amount    = plan.Price,
            Date      = DateTime.UtcNow,
            Status    = "paid",
        });

        // (F6) لا SaveChangesAsync — SubscriptionsController.Activate يضع .SaveAtEnd().
        return ToView(sub);
    }

    private static SubscriptionView ToView(SubscriptionEntity s) => new(
        Id:               s.Id.ToString(),
        PlanId:           s.PlanId.ToString(),
        PlanName:         s.PlanName,
        Status:           s.Status,
        StartDate:        s.StartDate,
        EndDate:          s.EndDate,
        ListingsLimit:    s.ListingsLimit,
        FeaturedLimit:    s.FeaturedLimit,
        ImagesPerListing: s.ImagesPerListing,
        Price:            0m,
        DaysRemaining:    Math.Max(0, (int)(s.EndDate - DateTime.UtcNow).TotalDays),
        ListingsUsed:     0,
        FeaturedUsed:     0);
}

public sealed class EjarPlanStore : IPlanStore
{
    private readonly EjarDbContext _db;
    public EjarPlanStore(EjarDbContext db) => _db = db;

    public async Task<IReadOnlyList<PlanView>> ListAsync(CancellationToken ct)
    {
        var rows = await _db.Plans.AsNoTracking().ToListAsync(ct);
        return rows.Select(ToView).ToList();
    }

    public async Task<PlanView?> GetAsync(string planId, CancellationToken ct)
    {
        if (!Guid.TryParse(planId, out var pid)) return null;
        var p = await _db.Plans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct);
        return p is null ? null : ToView(p);
    }

    private static PlanView ToView(PlanEntity p) => new(
        Id:               p.Id.ToString(),
        Name:             p.Label,
        Description:      p.Description ?? "",
        Price:            p.Price,
        Unit:             p.CycleLabel,
        ListingQuota:     p.MaxActiveListings,
        FeaturedQuota:    p.MaxFeaturedListings,
        ImagesPerListing: p.MaxImagesPerListing,
        Popular:          p.IsRecommended,
        Features:         string.IsNullOrEmpty(p.FeaturesCsv)
                            ? Array.Empty<string>()
                            : p.FeaturesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries));
}

public sealed class EjarInvoiceStore : IInvoiceStore
{
    private readonly EjarDbContext _db;
    public EjarInvoiceStore(EjarDbContext db) => _db = db;

    public async Task<IReadOnlyList<InvoiceView>> ListForUserAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return Array.Empty<InvoiceView>();

        var rows = await _db.Invoices.AsNoTracking()
            .Where(x => x.UserId == uid).OrderByDescending(x => x.Date)
            .Join(_db.Plans.AsNoTracking(),
                  inv => inv.PlanId,
                  plan => plan.Id,
                  (inv, plan) => new { inv, plan })
            .ToListAsync(ct);

        return rows.Select(r => new InvoiceView(
            Id:        r.inv.Id.ToString(),
            CreatedAt: r.inv.Date,
            Amount:    r.inv.Amount,
            Status:    r.inv.Status,
            Method:    "manual",
            PlanName:  r.plan?.Label,
            Reference: null)).ToList();
    }
}
