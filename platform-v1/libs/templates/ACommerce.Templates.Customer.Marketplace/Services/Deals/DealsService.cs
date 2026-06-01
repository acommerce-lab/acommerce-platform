using Marten;

namespace ACommerce.Templates.Customer.Marketplace.Services.Deals;

/// <summary>
/// خِدمَة الـ Deals — البَوّابَة الوَحيدَة لِتَغيير الحالَة. كُلّ المُتَطَلَّبات
/// تَمُرّ هُنا: فَحص الـ state machine، التَّحَقُّق مِن الفاعِل، الكِتابَة في
/// Timeline، حِفظ بِـ Marten. يُمكِن استِخدامُها مِن endpoints أَو
/// Wolverine handlers لاحِقاً.
/// </summary>
public sealed class DealsService
{
    private readonly IDocumentStore _store;
    public DealsService(IDocumentStore store) => _store = store;

    public async Task<Deal> StartAsync(
        string tenantSlug, string pattern,
        Guid initiatorId, string initiatorName,
        Guid? listingId, string listingTitle, decimal amountSar,
        Dictionary<string, string>? attributes = null,
        CancellationToken ct = default)
    {
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            TenantSlug = tenantSlug,
            Pattern = pattern,
            ListingId = listingId,
            ListingTitle = listingTitle,
            InitiatorId = initiatorId,
            InitiatorName = initiatorName,
            AmountSar = amountSar,
            Attributes = attributes ?? new(),
            Stage = DealStage.Offered,
            Status = DealStatus.Active,
            Timeline = new()
            {
                new(DealStage.Offered, DealStage.Offered, "created",
                    initiatorId, initiatorName, null, DateTime.UtcNow)
            }
        };
        await using var s = _store.LightweightSession(tenantSlug);
        s.Store(deal);
        await s.SaveChangesAsync(ct);
        return deal;
    }

    /// <summary>عَيِّن الطَّرَف الثاني (الـ counterparty) عَلى الـ Deal —
    /// عادَةً عِندَ مَرحَلَة Booked (بائِع يَقبَل، سائِق يَأخُذ الرحلَة، …).</summary>
    public async Task<Deal?> AssignCounterpartyAsync(
        string tenantSlug, Guid dealId,
        Guid counterpartyId, string counterpartyName,
        CancellationToken ct = default)
    {
        await using var s = _store.LightweightSession(tenantSlug);
        var deal = await s.LoadAsync<Deal>(dealId, ct);
        if (deal is null || deal.Status != DealStatus.Active) return deal;
        if (deal.CounterpartyId is not null) return deal;
        deal.CounterpartyId = counterpartyId;
        deal.CounterpartyName = counterpartyName;
        deal.UpdatedAt = DateTime.UtcNow;
        deal.Timeline.Add(new(deal.Stage, deal.Stage, "assigned",
            counterpartyId, counterpartyName, null, DateTime.UtcNow));
        s.Store(deal);
        await s.SaveChangesAsync(ct);
        return deal;
    }

    /// <summary>اِنتَقِل لِلمَرحَلَة التالِيَة في النَّمَط. يَفحَص أَنّ الفاعِل
    /// يَملِك حَقّ تَحريك هذه المَرحَلَة (initiator/counterparty/either).</summary>
    public async Task<DealAdvanceResult> AdvanceAsync(
        string tenantSlug, Guid dealId, Guid actorId, string actorName,
        string? note = null, CancellationToken ct = default)
    {
        await using var s = _store.LightweightSession(tenantSlug);
        var deal = await s.LoadAsync<Deal>(dealId, ct);
        if (deal is null) return new(false, null, "deal not found");
        if (deal.Status != DealStatus.Active)
            return new(false, deal, $"الصَّفقَة في حالَة {deal.Status} — لا يُمكِن المُتابَعَة.");

        var next = DealsPolicy.Next(deal.Pattern, deal.Stage);
        if (next is null)
            return new(false, deal, "هذه آخِر مَرحَلَة، لا تالٍ.");

        // فَحص الفاعِل: مَن المَسموح لَه بِتَحريك المَرحَلَة الحاليَّة؟
        var required = DealsPolicy.Actor(deal.Stage);
        if (!IsActorAllowed(deal, actorId, required))
            return new(false, deal, $"يَتَطَلَّب فاعِلاً مِن نَوع: {required}");

        var before = deal.Stage;
        deal.Stage = next.Value;
        deal.UpdatedAt = DateTime.UtcNow;
        deal.Timeline.Add(new(before, next.Value, "advanced",
            actorId, actorName, note, DateTime.UtcNow));

        // اكتمال الـ Deal عِندَ Reviewed.
        if (next == DealStage.Reviewed && DealsPolicy.StagesFor(deal.Pattern).Last() == DealStage.Reviewed)
        {
            deal.Status = DealStatus.Completed;
        }

        s.Store(deal);
        await s.SaveChangesAsync(ct);
        return new(true, deal, null);
    }

    public async Task<Deal?> CancelAsync(
        string tenantSlug, Guid dealId, Guid actorId, string actorName,
        string reason, CancellationToken ct = default)
    {
        await using var s = _store.LightweightSession(tenantSlug);
        var deal = await s.LoadAsync<Deal>(dealId, ct);
        if (deal is null || deal.Status != DealStatus.Active) return deal;
        deal.Status = DealStatus.Cancelled;
        deal.CancelReason = reason;
        deal.UpdatedAt = DateTime.UtcNow;
        deal.Timeline.Add(new(deal.Stage, deal.Stage, "cancelled",
            actorId, actorName, reason, DateTime.UtcNow));
        s.Store(deal);
        await s.SaveChangesAsync(ct);
        return deal;
    }

    public async Task<Deal?> DisputeAsync(
        string tenantSlug, Guid dealId, Guid actorId, string actorName,
        string reason, CancellationToken ct = default)
    {
        await using var s = _store.LightweightSession(tenantSlug);
        var deal = await s.LoadAsync<Deal>(dealId, ct);
        if (deal is null || deal.Status != DealStatus.Active) return deal;
        deal.Status = DealStatus.Disputed;
        deal.UpdatedAt = DateTime.UtcNow;
        deal.Timeline.Add(new(deal.Stage, deal.Stage, "disputed",
            actorId, actorName, reason, DateTime.UtcNow));
        s.Store(deal);
        await s.SaveChangesAsync(ct);
        return deal;
    }

    /// <summary>اِربِط كائِناً خارِجيّاً (مَثَلاً PaymentId بَعد الدَّفع).</summary>
    public async Task<Deal?> AttachRefAsync(
        string tenantSlug, Guid dealId, string key, string value,
        CancellationToken ct = default)
    {
        await using var s = _store.LightweightSession(tenantSlug);
        var deal = await s.LoadAsync<Deal>(dealId, ct);
        if (deal is null) return null;
        deal.Refs[key] = value;
        deal.UpdatedAt = DateTime.UtcNow;
        deal.Timeline.Add(new(deal.Stage, deal.Stage, "note", null, null,
            $"attach {key}={value}", DateTime.UtcNow));
        s.Store(deal);
        await s.SaveChangesAsync(ct);
        return deal;
    }

    public async Task<Deal?> LoadAsync(string tenantSlug, Guid dealId, CancellationToken ct = default)
    {
        await using var s = _store.QuerySession(tenantSlug);
        return await s.LoadAsync<Deal>(dealId, ct);
    }

    public async Task<List<Deal>> ListForUserAsync(
        string tenantSlug, Guid userId, CancellationToken ct = default)
    {
        await using var s = _store.QuerySession(tenantSlug);
        var all = await s.Query<Deal>()
            .Where(d => d.InitiatorId == userId || d.CounterpartyId == userId)
            .OrderByDescending(d => d.UpdatedAt)
            .Take(100).ToListAsync(ct);
        return all.ToList();
    }

    public async Task<List<Deal>> ListForTenantAsync(
        string tenantSlug, int take = 200, CancellationToken ct = default)
    {
        await using var s = _store.QuerySession(tenantSlug);
        var all = await s.Query<Deal>()
            .OrderByDescending(d => d.UpdatedAt).Take(take).ToListAsync(ct);
        return all.ToList();
    }

    static bool IsActorAllowed(Deal deal, Guid actorId, string required) => required switch
    {
        "initiator"    => deal.InitiatorId == actorId,
        "counterparty" => deal.CounterpartyId == actorId,
        "either"       => deal.InitiatorId == actorId || deal.CounterpartyId == actorId,
        "platform"     => true,   // يَستَدعيها كود مَنصَّة — افتَرِض السَّماح
        _              => false
    };
}

public sealed record DealAdvanceResult(bool Ok, Deal? Deal, string? Reason);
