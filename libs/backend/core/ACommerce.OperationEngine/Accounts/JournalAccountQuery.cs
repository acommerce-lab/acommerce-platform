using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Abstractions.Repositories;
using System.Text.Json;

namespace ACommerce.OperationEngine.Accounts;

/// <summary>
/// تنفيذ IAccountQuery فوق جدول journal_entries.
///
/// يستعلم عن الأطراف في الدفتر، يفكّ تسلسل بيانات JSON،
/// ويُعيد قائمة JournalParty قابلة للتجميع.
///
/// التسجيل:
///   services.AddScoped&lt;IAccountQuery, JournalAccountQuery&gt;();
///   (يُفعَّل تلقائياً ضمن AddOperationJournal)
/// </summary>
public class JournalAccountQuery : IAccountQuery
{
    private readonly IBaseAsyncRepository<JournalEntry> _repo;

    public JournalAccountQuery(IRepositoryFactory factory)
    {
        _repo = factory.CreateRepository<JournalEntry>();
    }

    public async Task<IReadOnlyList<JournalParty>> GetPartiesAsync(
        string identity,
        DateRange? dateRange = null,
        IEnumerable<(string Key, string Value)>? tags = null,
        CancellationToken ct = default)
    {
        var entries = await _repo.GetAllWithPredicateAsync(e =>
            !e.IsDeleted &&
            (dateRange == null || (e.Timestamp >= dateRange.From && e.Timestamp <= dateRange.To)));

        var result = new List<JournalParty>();
        var tagFilter = tags?.ToList();

        foreach (var entry in entries)
        {
            var parties = ParseParties(entry);
            foreach (var party in parties)
            {
                if (party.Identity != identity) continue;

                if (tagFilter is { Count: > 0 })
                {
                    if (!tagFilter.All(f => party.Tags.Any(t => t.Key == f.Key && t.Value == f.Value)))
                        continue;
                }

                result.Add(party);
            }
        }

        return result;
    }

    public async Task<decimal> GetBalanceAsync(
        string identity,
        Func<JournalParty, decimal>? valueAggregator = null,
        DateRange? dateRange = null,
        CancellationToken ct = default)
    {
        var parties = await GetPartiesAsync(identity, dateRange, null, ct);
        var aggregate = valueAggregator ?? (p => p.Value);
        return parties.Where(p => p.Success).Sum(aggregate);
    }

    // =========================================================================
    // التحليل الداخلي
    // =========================================================================

    private static List<JournalParty> ParseParties(JournalEntry entry)
    {
        var result = new List<JournalParty>();
        try
        {
            var raw = JsonSerializer.Deserialize<List<RawParty>>(entry.PartiesJson);
            if (raw == null) return result;

            foreach (var p in raw)
            {
                var tags = p.Tags?
                    .Select(t => (t.Key ?? string.Empty, t.Value ?? string.Empty))
                    .ToList()
                    as IReadOnlyList<(string Key, string Value)>
                    ?? [];

                result.Add(new JournalParty(
                    p.Identity ?? string.Empty,
                    p.Value,
                    entry.OperationType,
                    entry.OperationId,
                    entry.Timestamp,
                    entry.Success,
                    tags));
            }
        }
        catch (JsonException)
        {
            // إذا كان JSON تالفاً، تخطَّ هذا الإدخال
        }
        return result;
    }

    // DTOs داخلية لتفكيك JSON
    private sealed class RawParty
    {
        public string? Identity { get; set; }
        public decimal Value { get; set; }
        public List<RawTag>? Tags { get; set; }
    }

    private sealed class RawTag
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
    }
}
