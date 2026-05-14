using ACommerce.Kits.Reports.Domain;
using ACommerce.Kits.Reports.Operations;
using ACommerce.Kits.Subscriptions.Backend;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;
using V3ReportEntity = Ashare.V3.Domain.ReportEntity;

namespace Ashare.V3.Data.Stores;

// ════════════════════════════════════════════════════════════════════════
// مَخازِن V3 لِكيتات Reports + Subscriptions. تُسَجَّل في Program.cs:
//   .AddReports<AshareV3ReportStore>()
//   .AddSubscriptions<AshareV3SubscriptionStore, AshareV3PlanStore, AshareV3InvoiceStore>(
//       opts => opts.OpenAccess = true)
// ════════════════════════════════════════════════════════════════════════

/// <summary>
/// مُتَرجِم بَين Reports kit (IReport بِسَلاسِل) و V3 ReportEntity (مَزيج
/// string/Guid). asharedb.Reports موجود بِالحَقل <c>Description</c> بَدَل
/// <c>Body</c> و <c>EntityId</c> كَـ Guid لا string.
/// </summary>
public sealed class AshareV3ReportStore : IReportStore
{
    private readonly AshareV3DbContext _db;
    public AshareV3ReportStore(AshareV3DbContext db) => _db = db;

    [Obsolete("Use AddNoSaveAsync (kit-prescribed F6 flow).")]
    public Task<IReport> SubmitAsync(string reporterId, string entityType, string entityId,
                                     string reason, string? body, CancellationToken ct)
        => throw new NotSupportedException("Use AddNoSaveAsync.");

    public async Task AddNoSaveAsync(IReport r, CancellationToken ct)
    {
        if (!Guid.TryParse(r.EntityId, out var entityId)) return;
        _db.Reports.Add(new V3ReportEntity
        {
            Id          = Guid.TryParse(r.Id, out var id) ? id : Guid.NewGuid(),
            CreatedAt   = r.CreatedAt == default ? DateTime.UtcNow : r.CreatedAt,
            ReporterId  = r.ReporterId,            // AspNetUsers.Id string
            EntityType  = string.IsNullOrEmpty(r.EntityType) ? "Listing" : r.EntityType,
            EntityId    = entityId,
            Reason      = r.Reason,
            Description = r.Body,
            Status      = string.IsNullOrEmpty(r.Status) ? "pending" : r.Status,
        });
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<IReport>> ListMineAsync(string userId, CancellationToken ct)
    {
        var rows = await _db.Reports.AsNoTracking()
            .Where(r => r.ReporterId == userId)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return rows.Select(ToView).ToList();
    }

    public async Task<IReadOnlyList<IReport>> ListAllAsync(string? status, CancellationToken ct)
    {
        var q = _db.Reports.AsNoTracking();
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);
        var rows = await q.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return rows.Select(ToView).ToList();
    }

    public async Task<bool> SetStatusAsync(string reportId, string newStatus, CancellationToken ct)
    {
        if (!Guid.TryParse(reportId, out var id)) return false;
        var r = await _db.Reports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;
        r.Status = newStatus;
        if (newStatus is "resolved" or "dismissed") r.ResolvedAt = DateTime.UtcNow;
        r.UpdatedAt = DateTime.UtcNow;
        // (F6) لا SaveChangesAsync — ReportsController.SetStatus يَضَع .SaveAtEnd().
        return true;
    }

    private static IReport ToView(V3ReportEntity r) => new InMemoryReport(
        Id:         r.Id.ToString(),
        ReporterId: r.ReporterId,
        EntityType: r.EntityType,
        EntityId:   r.EntityId.ToString(),
        Reason:     r.Reason,
        Body:       r.Description,
        Status:     r.Status,
        CreatedAt:  r.CreatedAt,
        UpdatedAt:  r.UpdatedAt);
}


// ─── Subscriptions ──────────────────────────────────────────────────────
// V3 لا يَملِك جَداوِل Subscriptions/Plans/Invoices في asharedb. نَستَخدِم:
//   - OpenAccess=true لِيُعطي كُلّ مُستَخدِم اشتِراك اصطِناعي نَشِط
//   - PlanStore يَعرِض قائِمَة باقات seeded تُطابِق ما كان عَشير V1 يَعرِض
//   - SubscriptionStore + InvoiceStore in-memory (لا persistence — V3 لا
//     يَستَخدِم باقات حَقيقِيَّة الآن)

public sealed class AshareV3PlanStore : IPlanStore
{
    private static readonly IReadOnlyList<PlanView> Plans = new[]
    {
        new PlanView(
            Id: "trial",
            Name: "تجربة مفتوحة",
            Description: "للمستخدمين الجُدُد — استَكشِف كُلّ المَزايا بِلا قُيود لِفَترَة مَحدودَة.",
            Price: 0m,
            Unit: "monthly",
            ListingQuota: 0,                  // 0 = unlimited
            FeaturedQuota: 0,
            ImagesPerListing: 10,
            Popular: false,
            Features: new[] { "كُلّ المَزايا الأَساسِيَّة", "صُوَر مُتَعَدِّدَة", "بِلا حُدود إعلانات", "دَعم أَوَّلي" }),
        new PlanView(
            Id: "basic",
            Name: "أَساسي",
            Description: "لِلمُلّاك المُستَقِلّين — يَكفي لِنَشر إعلانات مَنزِلِيَّة بِدون عَبء.",
            Price: 9_900m,
            Unit: "monthly",
            ListingQuota: 5,
            FeaturedQuota: 1,
            ImagesPerListing: 5,
            Popular: false,
            Features: new[] { "حَتّى 5 إعلانات", "إعلان مُمَيَّز واحِد", "5 صُوَر لِكُلّ إعلان", "دَعم بِالبَريد" }),
        new PlanView(
            Id: "pro",
            Name: "احتِرافي",
            Description: "لِلوُكَلاء العَقارِيِّين — أَدَوات تَسويق إضافِيَّة + إعلانات مُمَيَّزَة.",
            Price: 29_900m,
            Unit: "monthly",
            ListingQuota: 30,
            FeaturedQuota: 5,
            ImagesPerListing: 10,
            Popular: true,
            Features: new[] { "حَتّى 30 إعلان", "5 إعلانات مُمَيَّزَة", "10 صُوَر لِكُلّ إعلان", "إحصاءات تَفصيلِيَّة", "دَعم سَريع" }),
        new PlanView(
            Id: "business",
            Name: "أَعمال",
            Description: "لِلشَّرِكات والمَكاتِب — كُلّ المَزايا بِلا قُيود.",
            Price: 79_900m,
            Unit: "monthly",
            ListingQuota: 0,
            FeaturedQuota: 20,
            ImagesPerListing: 15,
            Popular: false,
            Features: new[] { "إعلانات غَير مَحدودَة", "20 إعلان مُمَيَّز", "15 صورة لِكُلّ إعلان", "مَدير حِساب مُخَصَّص", "دَعم 24/7", "API access" }),
    };

    public Task<IReadOnlyList<PlanView>> ListAsync(CancellationToken ct) =>
        Task.FromResult(Plans);

    public Task<PlanView?> GetAsync(string planId, CancellationToken ct) =>
        Task.FromResult(Plans.FirstOrDefault(p => p.Id == planId));
}

/// <summary>
/// In-memory store — V3 لا يَتَتَبَّع اشتِراكات حَقيقِيَّة الآن. OpenAccess
/// في options يُعطي اشتِراك اصطِناعي نَشِط مِن داخِل الـ controller، فَلا
/// يَستَدعي هذا الـ store إلّا في activation paths (تَنجَح صَوريّاً).
/// </summary>
public sealed class AshareV3SubscriptionStore : ISubscriptionStore
{
    private readonly IPlanStore _plans;
    public AshareV3SubscriptionStore(IPlanStore plans) => _plans = plans;

    public Task<SubscriptionView?> GetActiveAsync(string userId, CancellationToken ct) =>
        Task.FromResult<SubscriptionView?>(null); // OpenAccess يَتَكَفَّل بِالاصطِناعي

    public async Task<SubscriptionView> ActivateAsync(string userId, string planId, CancellationToken ct)
    {
        var plan = await _plans.GetAsync(planId, ct);
        var now = DateTime.UtcNow;
        return new SubscriptionView(
            Id:               Guid.NewGuid().ToString(),
            PlanId:           planId,
            PlanName:         plan?.Name ?? "—",
            Status:           "active",
            StartDate:        now,
            EndDate:          now.AddMonths(1),
            ListingsLimit:    plan?.ListingQuota ?? 0,
            FeaturedLimit:    plan?.FeaturedQuota ?? 0,
            ImagesPerListing: plan?.ImagesPerListing ?? 0,
            Price:            plan?.Price ?? 0m,
            DaysRemaining:    30);
    }
}

public sealed class AshareV3InvoiceStore : IInvoiceStore
{
    public Task<IReadOnlyList<InvoiceView>> ListForUserAsync(string userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InvoiceView>>(Array.Empty<InvoiceView>());
}
