using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.OperationEngine.Core;

namespace ACommerce.Authentication.TwoFactor.Operations.Analyzers;

/// <summary>
/// محلل حالة تحدي 2FA - يفحص أن التحدي:
/// - موجود في المخزن
/// - لم تنته صلاحيته
/// - لم يتجاوز عدد المحاولات
///
/// عند النجاح يضع Challenge في الـ context تحت "challenge".
/// </summary>
public class ChallengeStateAnalyzer : IOperationAnalyzer
{
    private readonly IChallengeStore _store;
    private readonly string _challengeId;

    public string Name => $"ChallengeState({_challengeId})";

    public IReadOnlyList<string> WatchedTagKeys => new[] { TwoFactorTags.Challenge.Name };

    public ChallengeStateAnalyzer(IChallengeStore store, string challengeId)
    {
        _store = store;
        _challengeId = challengeId;
    }

    public async Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
    {
        var existing = await _store.GetAsync(_challengeId, context.CancellationToken);

        if (existing == null)
            return AnalyzerResult.Fail("challenge_not_found");

        if (existing.IsExpired)
        {
            await _store.UpdateStatusAsync(_challengeId, ChallengeStatus.Expired, context.CancellationToken);
            return AnalyzerResult.Fail("challenge_expired");
        }

        if (existing.ExceededAttempts)
            return AnalyzerResult.Fail($"too_many_attempts: {existing.AttemptCount}/{existing.MaxAttempts}");

        context.Set("challenge", existing);

        return new AnalyzerResult
        {
            Passed = true,
            Message = "challenge_valid",
            Data = new Dictionary<string, object>
            {
                ["challengeId"] = existing.Id,
                ["channelName"] = existing.ChannelName,
                ["status"] = existing.Status.ToString()
            }
        };
    }
}
