using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ACommerce.Authentication.TwoFactor.Providers.Nafath.Mock;

/// <summary>
/// قَناة نَفاذ تَجريبيَّة. تَستَخدِم <see cref="MockNafathOptions"/> لِلتَّحَكُّم
/// بِرَقم العَرض (الذي يَجِب أَن يَضغَطه المُستَخدِم في تَطبيق نَفاذ
/// الحَقيقي) وزَمَن التَّحَقُّق التِلقائي.
///
/// السُّلوك:
/// <list type="bullet">
///   <item><c>InitiateAsync</c> → يُخَزِّن تَحَدّياً ويُرجِع <c>DisplayCode</c>
///         الثابِت في <c>ProviderData["displayCode"]</c> لِيَعرِضه الـ frontend.</item>
///   <item><c>VerifyAsync</c> → يَنجَح تِلقائيّاً بَعد <c>AutoVerifySeconds</c>
///         (مُحاكاة ضَغط المُستَخدِم في نَفاذ الحَقيقي). لا يَتَطَلَّب كوداً
///         مِن المُستَخدِم.</item>
///   <item>التَّحَقُّق idempotent: بَعد النَّجاح يَبقى التَّحَدّي حَتّى انتِهاء
///         <c>ExpirySeconds</c> لِيَسمَح بِاستِعلامات polling مُتَكَرِّرَة.</item>
/// </list>
///
/// تُستَبدَل بِـ <c>NafathTwoFactorChannel</c> في الإنتاج.
/// </summary>
public sealed class MockNafathTwoFactorChannel : ITwoFactorChannel
{
    private readonly MockNafathOptions _options;

    public MockNafathTwoFactorChannel(IOptions<MockNafathOptions> options)
    {
        _options = options.Value ?? new MockNafathOptions();
    }

    /// <summary>مُنشئ بِلا خِيارات — يَستَخدِم الافتِراضِيّات (DisplayCode="00"، 10s).</summary>
    public MockNafathTwoFactorChannel() : this(Options.Create(new MockNafathOptions())) { }

    private record PendingChallenge(
        string Identifier,
        string DisplayCode,
        DateTimeOffset InitiatedAt,
        DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, PendingChallenge> _pending = new();

    public string Name => "nafath";
    public bool GeneratesCode => false; // نَفاذ لا يُرسِل كوداً — يَعرِض رَقماً لِلمُستَخدِم

    public Task<ChallengeResult> InitiateAsync(
        string userIdentifier,
        string? target = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userIdentifier))
            return Task.FromResult(new ChallengeResult(false, "", "identifier_required"));

        var challengeId = Guid.NewGuid().ToString("N");
        var displayCode = _options.DisplayCode;
        var now = DateTimeOffset.UtcNow;

        _pending[challengeId] = new PendingChallenge(
            Identifier:  userIdentifier,
            DisplayCode: displayCode,
            InitiatedAt: now,
            ExpiresAt:   now.AddSeconds(_options.ExpirySeconds));

        return Task.FromResult(new ChallengeResult(
            Succeeded: true,
            ChallengeId: challengeId,
            ProviderData: new Dictionary<string, string>
            {
                ["displayCode"]      = displayCode,
                ["expiresInSeconds"] = _options.ExpirySeconds.ToString(),
                ["autoVerifyInSeconds"] = _options.AutoVerifySeconds.ToString(),
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

        // التَّحَقُّق التِلقائي بَعد AutoVerifySeconds
        if ((now - p.InitiatedAt).TotalSeconds >= _options.AutoVerifySeconds)
        {
            // idempotent: لا نَحذِف — نَسمَح بِالاستِعلام المُتَكَرِّر حَتّى انتِهاء العُمر
            return Task.FromResult(new VerificationResult(
                Verified: true,
                ProviderData: new Dictionary<string, string> { ["identifier"] = p.Identifier }));
        }

        var remaining = _options.AutoVerifySeconds - (int)(now - p.InitiatedAt).TotalSeconds;
        return Task.FromResult(new VerificationResult(false, $"pending:{remaining}"));
    }
}
