using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using System.Collections.Concurrent;

namespace ACommerce.Authentication.TwoFactor.Providers.Nafath.Mock;

/// <summary>
/// قناة نفاذ تجريبية.
///
/// السلوك:
/// <list type="bullet">
///   <item>InitiateAsync → يولّد رمزاً عشوائياً (10–99) ويُرجعه في ProviderData["displayCode"].</item>
///   <item>VerifyAsync   → يتحقق تلقائياً بعد 10 ثوانٍ من بدء التحدي (لا يحتاج كوداً من المستخدم).</item>
///   <item>التحقق idempotent: بعد النجاح تُبقي التحدي للسماح بالاستعلام مرات متعددة.</item>
/// </list>
///
/// تُستبدَل بـ <c>NafathTwoFactorChannel</c> في الإنتاج.
/// </summary>
public sealed class MockNafathTwoFactorChannel : ITwoFactorChannel
{
    private const int AutoVerifySeconds = 10;
    private const int ExpirySeconds     = 120;

    private record PendingChallenge(
        string Identifier,
        string DisplayCode,
        DateTimeOffset InitiatedAt,
        DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, PendingChallenge> _pending = new();

    public string Name => "nafath";
    public bool GeneratesCode => false; // نفاذ لا يُرسِل كوداً — يعرض رقماً للمستخدم

    public Task<ChallengeResult> InitiateAsync(
        string userIdentifier,
        string? target = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userIdentifier))
            return Task.FromResult(new ChallengeResult(false, "", "identifier_required"));

        var challengeId = Guid.NewGuid().ToString("N");
        var displayCode = Random.Shared.Next(10, 100).ToString();
        var now = DateTimeOffset.UtcNow;

        _pending[challengeId] = new PendingChallenge(
            Identifier:  userIdentifier,
            DisplayCode: displayCode,
            InitiatedAt: now,
            ExpiresAt:   now.AddSeconds(ExpirySeconds));

        return Task.FromResult(new ChallengeResult(
            Succeeded: true,
            ChallengeId: challengeId,
            ProviderData: new Dictionary<string, string>
            {
                ["displayCode"]      = displayCode,
                ["expiresInSeconds"] = ExpirySeconds.ToString(),
            }));
    }

    public Task<VerificationResult> VerifyAsync(
        string challengeId,
        string? providedCode = null,
        CancellationToken ct = default)
    {
        if (!_pending.TryGetValue(challengeId, out var p))
            return Task.FromResult(new VerificationResult(false, "challenge_not_found"));

        var now = DateTimeOffset.UtcNow;

        if (now > p.ExpiresAt)
        {
            _pending.TryRemove(challengeId, out _);
            return Task.FromResult(new VerificationResult(false, "expired"));
        }

        // التحقق التلقائي بعد AutoVerifySeconds
        if ((now - p.InitiatedAt).TotalSeconds >= AutoVerifySeconds)
        {
            // idempotent: لا نحذف — نسمح بالاستعلام المتكرر حتى انتهاء العمر
            return Task.FromResult(new VerificationResult(
                Verified: true,
                ProviderData: new Dictionary<string, string> { ["identifier"] = p.Identifier }));
        }

        var remaining = AutoVerifySeconds - (int)(now - p.InitiatedAt).TotalSeconds;
        return Task.FromResult(new VerificationResult(false, $"pending:{remaining}"));
    }
}
