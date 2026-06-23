using Marten;
using Microsoft.Extensions.Configuration;

namespace ACommerce.Templates.Customer.Marketplace.Services;

// ════════════════════════════════════════════════════════════════════════
//  حِصَص استِخدام وُكلاء الذَّكاء — حِماية مُزدَوَجَة:
//    ١) لِكُلّ مُستَخدِم: حَدّ يَوميّ + أُسبوعيّ (عَدالَة بَين رُوَّاد الأَعمال)
//    ٢) لِكُلّ المَنصَّة: صَمّام أَمان يَوميّ (يَحمي سَقف مُزَوِّد الـ LLM)
//
//  القاعِدَة: نَفحَص قَبل النِّداء (GetStatus)، ونَستَهلِك بَعد النَّجاح فَقَط
//  (Consume) — لا نَحرِق حِصَّة المُستَخدِم عَلى أَخطاء الشَّبَكَة/المُزَوِّد.
//  التَّخزين في tenant الإدارَة (_admin) مِثل AgentSession.
// ════════════════════════════════════════════════════════════════════════

/// <summary>سِجِلّ حِصَّة مُستَخدِم واحِد لِنَوع وَكيل واحِد.</summary>
public sealed class AgentQuotaRecord
{
    /// <summary>"{userId:N}:{kind}" مَثَلاً "ab12…:analysis".</summary>
    public string Id { get; set; } = "";
    public int DayCount { get; set; }
    public string DayStamp { get; set; } = "";    // "yyyy-MM-dd" (UTC)
    public int WeekCount { get; set; }
    public string WeekStamp { get; set; } = "";   // "yyyy-Www" (ISO)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>صَمّام الأَمان عَلى مُستَوى المَنصَّة (يَوميّ فَقَط).</summary>
public sealed class AgentQuotaPlatformRecord
{
    /// <summary>"platform:{kind}".</summary>
    public string Id { get; set; } = "";
    public int DayCount { get; set; }
    public string DayStamp { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>حالَة الحِصَّة لِلعَرض + قَرار السَّماح.</summary>
public sealed record QuotaStatus(
    bool Allowed,
    int DayUsed, int DayLimit,
    int WeekUsed, int WeekLimit,
    string? DeniedReason);

public sealed class AgentQuotaService
{
    private readonly IDocumentStore _store;
    private readonly IConfiguration _cfg;
    private const string AdminTenant = "_admin";

    public AgentQuotaService(IDocumentStore store, IConfiguration cfg)
    {
        _store = store;
        _cfg = cfg;
    }

    /// <summary>المِفتاح الرَّئيس لِلتَّفعيل — يُمكِن إيقاف الحِصَص كُلّيّاً
    /// بِـ Quota:Enabled=false (مَثَلاً في التَّطوير).</summary>
    public bool Enabled => !string.Equals(_cfg["Quota:Enabled"], "false", StringComparison.OrdinalIgnoreCase);

    // الحُدود الافتراضيَّة (باقَة Spark المَجّانيَّة) — قابِلَة لِلتَّجاوُز
    // عَبر الإعدادات، ولاحِقاً حَسَب باقَة المُستَخدِم.
    private int DayLimit(string kind)      => Int($"Quota:{Cap(kind)}:PerDay",          kind == "analysis" ? 2  : 6);
    private int WeekLimit(string kind)     => Int($"Quota:{Cap(kind)}:PerWeek",         kind == "analysis" ? 7  : 30);
    private int PlatformDayLimit(string kind) => Int($"Quota:{Cap(kind)}:PlatformPerDay", kind == "analysis" ? 40 : 200);

    private int Int(string key, int dflt) => int.TryParse(_cfg[key], out var v) ? v : dflt;
    private static string Cap(string kind) => kind == "analysis" ? "Analysis" : "Design";

    private static string TodayStamp() => DateTime.UtcNow.ToString("yyyy-MM-dd");
    private static string WeekStamp()
    {
        var now = DateTime.UtcNow;
        var week = System.Globalization.ISOWeek.GetWeekOfYear(now);
        return $"{System.Globalization.ISOWeek.GetYear(now)}-W{week:D2}";
    }

    /// <summary>يَفحَص الحالَة دونَ تَعديل — لِلعَرض وَلِلفَحص المُسبَق.</summary>
    public async Task<QuotaStatus> GetStatusAsync(Guid userId, string kind, CancellationToken ct = default)
    {
        var dayLimit = DayLimit(kind);
        var weekLimit = WeekLimit(kind);
        if (!Enabled)
            return new QuotaStatus(true, 0, dayLimit, 0, weekLimit, null);

        await using var s = _store.QuerySession(AdminTenant);
        var rec = await s.LoadAsync<AgentQuotaRecord>($"{userId:N}:{kind}", ct);

        var today = TodayStamp();
        var week = WeekStamp();
        var dayUsed  = (rec is not null && rec.DayStamp == today)  ? rec.DayCount  : 0;
        var weekUsed = (rec is not null && rec.WeekStamp == week)  ? rec.WeekCount : 0;

        if (dayUsed >= dayLimit)
            return new QuotaStatus(false, dayUsed, dayLimit, weekUsed, weekLimit,
                $"بَلَغتَ حَدّ اليَوم ({dayLimit}). المُتَبَقّي هذا الأُسبوع: {Math.Max(0, weekLimit - weekUsed)}. جَدِّد غَداً أَو رَقِّ باقَتَك.");
        if (weekUsed >= weekLimit)
            return new QuotaStatus(false, dayUsed, dayLimit, weekUsed, weekLimit,
                $"بَلَغتَ حَدّ الأُسبوع ({weekLimit}). يَتَجَدَّد مَطلَع الأُسبوع القادِم، أَو رَقِّ باقَتَك.");

        // صَمّام الأَمان عَلى مُستَوى المَنصَّة.
        var plat = await s.LoadAsync<AgentQuotaPlatformRecord>($"platform:{kind}", ct);
        var platDay = (plat is not null && plat.DayStamp == today) ? plat.DayCount : 0;
        if (platDay >= PlatformDayLimit(kind))
            return new QuotaStatus(false, dayUsed, dayLimit, weekUsed, weekLimit,
                "النِّظام بَلَغَ حَدَّه اليَوميّ لِهذِه الخِدمَة. حاوِل بَعد قَليل أَو غَداً.");

        return new QuotaStatus(true, dayUsed, dayLimit, weekUsed, weekLimit, null);
    }

    /// <summary>يُسَجِّل استِهلاكاً واحِداً — يُستَدعى بَعد نَجاح النِّداء فَقَط.
    /// يُدير تَدوير الطَّوابِع (يَوم/أُسبوع) تِلقائيّاً.</summary>
    public async Task ConsumeAsync(Guid userId, string kind, CancellationToken ct = default)
    {
        if (!Enabled) return;
        await using var s = _store.LightweightSession(AdminTenant);
        var today = TodayStamp();
        var week = WeekStamp();

        var rec = await s.LoadAsync<AgentQuotaRecord>($"{userId:N}:{kind}", ct)
                  ?? new AgentQuotaRecord { Id = $"{userId:N}:{kind}" };
        rec.DayCount  = (rec.DayStamp  == today) ? rec.DayCount  + 1 : 1;
        rec.DayStamp  = today;
        rec.WeekCount = (rec.WeekStamp == week)  ? rec.WeekCount + 1 : 1;
        rec.WeekStamp = week;
        rec.UpdatedAt = DateTime.UtcNow;
        s.Store(rec);

        var plat = await s.LoadAsync<AgentQuotaPlatformRecord>($"platform:{kind}", ct)
                   ?? new AgentQuotaPlatformRecord { Id = $"platform:{kind}" };
        plat.DayCount = (plat.DayStamp == today) ? plat.DayCount + 1 : 1;
        plat.DayStamp = today;
        plat.UpdatedAt = DateTime.UtcNow;
        s.Store(plat);

        await s.SaveChangesAsync(ct);
    }
}
