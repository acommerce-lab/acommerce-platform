using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Operations.Operations;
using ACommerce.OperationEngine.Core;

namespace ACommerce.Authentication.TwoFactor.Operations;

/// <summary>
/// تهيئة المصادقة الثنائية.
///
/// services.AddTwoFactor(config => {
///     config.AddChannel(new SmsTwoFactorChannel(...));
///     config.AddChannel(new EmailTwoFactorChannel(...));
///     config.AddChannel(new NafathTwoFactorChannel(...));
///     config.UseStore(new InMemoryChallengeStore());
/// });
/// </summary>
public class TwoFactorConfig
{
    internal Dictionary<string, ITwoFactorChannel> Channels { get; } = new();
    internal IChallengeStore? Store { get; private set; }

    public TwoFactorConfig AddChannel(ITwoFactorChannel channel)
    {
        Channels[channel.Name] = channel;
        return this;
    }

    public TwoFactorConfig UseStore(IChallengeStore store)
    {
        Store = store;
        return this;
    }
}

/// <summary>
/// واجهة المطور البسيطة للـ 2FA.
///
///   var ch = await tfa.InitiateAsync("sms", userId: "123", target: "+966...");
///   // ... user enters code ...
///   var result = await tfa.VerifyAsync("sms", ch.ChallengeId, code: "123456");
/// </summary>
public class TwoFactorService
{
    private readonly TwoFactorConfig _config;
    private readonly OpEngine _engine;

    public TwoFactorService(TwoFactorConfig config, OpEngine engine)
    {
        _config = config;
        _engine = engine;
    }

    /// <summary>إطلاق تحدي</summary>
    public async Task<ChallengeResult> InitiateAsync(
        string channelName,
        string userId,
        string? target = null,
        CancellationToken ct = default)
    {
        if (!_config.Channels.TryGetValue(channelName, out var channel))
            throw new ArgumentException($"2FA channel '{channelName}' not registered.");

        var op = TwoFactorOps.Initiate(TwoFactorPartyId.User(userId), channel, target, _config.Store);
        var result = await _engine.ExecuteAsync(op, ct);

        if (!result.Success)
        {
            result.Context!.TryGet<string>("error", out var err);
            return new ChallengeResult(Succeeded: false, ChallengeId: "", Error: err);
        }

        result.Context!.TryGet<string>("challengeId", out var chId);
        return new ChallengeResult(Succeeded: true, ChallengeId: chId ?? "");
    }

    /// <summary>التحقق من تحدي</summary>
    public async Task<VerificationResult> VerifyAsync(
        string channelName,
        string userId,
        string challengeId,
        string? code = null,
        CancellationToken ct = default)
    {
        if (!_config.Channels.TryGetValue(channelName, out var channel))
            throw new ArgumentException($"2FA channel '{channelName}' not registered.");

        var op = TwoFactorOps.Verify(TwoFactorPartyId.User(userId), challengeId, channel, code, _config.Store);
        var result = await _engine.ExecuteAsync(op, ct);

        result.Context!.TryGet<bool>("verified", out var verified);
        result.Context!.TryGet<string>("reason", out var reason);

        return new VerificationResult(Verified: verified && result.Success, Reason: reason);
    }

    /// <summary>إلغاء تحدي</summary>
    public async Task ExpireAsync(string challengeId, CancellationToken ct = default)
    {
        var op = TwoFactorOps.Expire(challengeId, _config.Store);
        await _engine.ExecuteAsync(op, ct);
    }
}
