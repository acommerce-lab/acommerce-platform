using System.Text.Json;
using Marten;

namespace ACommerce.Templates.Customer.Marketplace.Services.Incubator;

/// <summary>
/// محرّك التحليل الاستثماري. يبني الـ prompt، يستدعي الـ LLM backend
/// الموجود، يتحقق من JSON (مع إعادة محاولة)، يحسب درجة جودة، ويحفظ على
/// <see cref="IncubatorSession"/>. يُخزَّن تحت tenant ثابت "_incubator".
/// </summary>
public sealed class FeasibilityAnalysisService
{
    public const string IncubatorTenant = "_incubator";

    private readonly IDocumentStore _store;
    private readonly IAgentBackend _backend;
    private readonly FeasibilityPromptBuilder _prompt;

    public FeasibilityAnalysisService(
        IDocumentStore store, IAgentBackend backend, FeasibilityPromptBuilder prompt)
    {
        _store = store;
        _backend = backend;
        _prompt = prompt;
    }

    public bool IsConfigured => _backend.IsConfigured;

    // ─── Session lifecycle ──────────────────────────────────────────
    public async Task<IncubatorSession> StartAsync(Guid userId, string userName, CancellationToken ct = default)
    {
        var s = new IncubatorSession
        {
            Id = Guid.NewGuid(), OwnerUserId = userId, OwnerName = userName,
            Status = IncubatorStatus.Discovery
        };
        await using var session = _store.LightweightSession(IncubatorTenant);
        session.Store(s);
        await session.SaveChangesAsync(ct);
        return s;
    }

    public async Task<IncubatorSession?> LoadAsync(Guid id, CancellationToken ct = default)
    {
        await using var session = _store.QuerySession(IncubatorTenant);
        return await session.LoadAsync<IncubatorSession>(id, ct);
    }

    public async Task<List<IncubatorSession>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var session = _store.QuerySession(IncubatorTenant);
        return (await session.Query<IncubatorSession>()
            .Where(x => x.OwnerUserId == userId)
            .OrderByDescending(x => x.UpdatedAt).ToListAsync(ct)).ToList();
    }

    public async Task SaveAnswerAsync(Guid id, string questionId, string answer, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession(IncubatorTenant);
        var s = await session.LoadAsync<IncubatorSession>(id, ct);
        if (s is null) return;
        if (questionId == "description") s.ProjectDescription = answer;
        else s.Answers[questionId] = answer;
        s.UpdatedAt = DateTime.UtcNow;

        // عند آخر سؤال، احسب النمط المقترح وبدّل الحالة.
        var answeredCount = s.Answers.Count + (string.IsNullOrEmpty(s.ProjectDescription) ? 0 : 1);
        if (answeredCount >= DiscoveryQuestionBank.Count)
        {
            var suggestion = PatternMatcher.Match(s.Answers);
            s.SuggestedPattern = suggestion.Pattern;
            s.PatternConfidence = suggestion.Confidence;
            s.PatternReasoning = suggestion.ReasoningAr;
            s.Status = IncubatorStatus.PatternSuggested;
        }
        session.Store(s);
        await session.SaveChangesAsync(ct);
    }

    // ─── Analysis ───────────────────────────────────────────────────
    /// <summary>يشغّل التحليل: prompt → LLM → JSON صالح → حفظ. يُعيد الجلسة المُحدَّثة.</summary>
    public async Task<IncubatorSession> RunAnalysisAsync(Guid id, CancellationToken ct = default)
    {
        var s = await LoadAsync(id, ct);
        if (s is null) throw new InvalidOperationException("session not found");

        await SetStatusAsync(id, IncubatorStatus.Analyzing, ct);

        var sector = s.Answers.TryGetValue("sector", out var sec) ? sec : "other";
        var systemPrompt = _prompt.Build(s, _prompt.FailuresForSector(sector));
        var userMsg = FeasibilityPromptBuilder.BuildUserMessage(s);

        string? json = null;
        string? lastError = null;
        for (var attempt = 0; attempt < 2 && json is null; attempt++)
        {
            var messages = new List<AgentMessage>
            {
                new("user", attempt == 0 ? userMsg
                    : userMsg + "\n\n[تذكير: أعد JSON صالحاً فقط مطابقاً للـ schema، بلا أي نص آخر.]",
                    null, null)
            };
            var req = new AgentRequest(systemPrompt, messages,
                Array.Empty<AgentToolDef>(), _backend.DefaultModel, MaxTokens: 8000);
            var resp = await _backend.CallAsync(req, ct);
            if (resp.Error is not null) { lastError = resp.Error; continue; }
            json = ExtractJson(resp.Text);
            if (json is null) lastError = "الردّ لم يكن JSON صالحاً.";
        }

        await using var session = _store.LightweightSession(IncubatorTenant);
        var fresh = await session.LoadAsync<IncubatorSession>(id, ct) ?? s;
        fresh.PromptVersion = FeasibilityPromptBuilder.Version;
        fresh.UpdatedAt = DateTime.UtcNow;
        if (json is null)
        {
            fresh.Status = IncubatorStatus.Failed;
            fresh.AnalysisError = lastError ?? "فشل غير معروف.";
        }
        else
        {
            fresh.AnalysisJson = json;
            fresh.AnalysisQualityScore = ScoreQuality(json, sector);
            fresh.AnalysisError = null;
            fresh.Status = IncubatorStatus.Completed;
        }
        session.Store(fresh);
        await session.SaveChangesAsync(ct);
        return fresh;
    }

    private async Task SetStatusAsync(Guid id, IncubatorStatus status, CancellationToken ct)
    {
        await using var session = _store.LightweightSession(IncubatorTenant);
        var s = await session.LoadAsync<IncubatorSession>(id, ct);
        if (s is null) return;
        s.Status = status; s.UpdatedAt = DateTime.UtcNow;
        session.Store(s);
        await session.SaveChangesAsync(ct);
    }

    // ─── Helpers ─────────────────────────────────────────────────────
    /// <summary>يستخرج كتلة JSON من ردّ قد يحوي markdown fences أو نصاً حوله.</summary>
    internal static string? ExtractJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        // أزل ```json ... ``` إن وُجِدت.
        if (t.StartsWith("```"))
        {
            var firstNl = t.IndexOf('\n');
            if (firstNl > 0) t = t[(firstNl + 1)..];
            if (t.EndsWith("```")) t = t[..^3];
            t = t.Trim();
        }
        var start = t.IndexOf('{');
        var end = t.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        var candidate = t.Substring(start, end - start + 1);
        try { using var _ = JsonDocument.Parse(candidate); return candidate; }
        catch { return null; }
    }

    /// <summary>درجة جودة 0-100: اكتمال الأقسام + استخدام السياق السعودي.</summary>
    internal static int ScoreQuality(string json, string sector)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string[] required = { "summary", "marketSizing", "customerSegments", "competitors",
                "revenueModel", "costStructure", "financialProjection", "risks",
                "lessonsFromFailures", "roadmap", "kpis", "recommendations" };
            var present = required.Count(k => root.TryGetProperty(k, out _));
            var completeness = (int)(present / (double)required.Length * 70);

            // إشارات السياق المحلي: ذكر "ريال" أو "السعودي" أو مخاطر تنظيمية.
            var raw = json;
            var localSignals = 0;
            if (raw.Contains("ريال")) localSignals += 10;
            if (raw.Contains("regulatory") || raw.Contains("تنظيم")) localSignals += 10;
            if (root.TryGetProperty("lessonsFromFailures", out var lf)
                && lf.ValueKind == JsonValueKind.Array && lf.GetArrayLength() > 0) localSignals += 10;

            return Math.Min(100, completeness + localSignals);
        }
        catch { return 0; }
    }
}
