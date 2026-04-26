using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Operations.Analyzers;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Authentication.TwoFactor.Operations.Operations;

/// <summary>
/// قيود المصادقة الثنائية.
///
/// Initiate: المستخدم يطلب → القناة تُصدر تحدياً.
/// Verify: المستخدم يُقدم الكود/الرد → التحدي يُسلّم مصادقة.
/// Expire: تحدي → انتهاء صلاحية (إلغاء).
/// </summary>
public static class TwoFactorOps
{
    /// <summary>
    /// قيد: إطلاق تحدي 2FA.
    /// المستخدم (مدين) يطلب تحدياً → القناة (دائن) تُصدر.
    /// </summary>
    public static Operation Initiate(
        TwoFactorPartyId user,
        ITwoFactorChannel channel,
        string? target = null,
        IChallengeStore? store = null)
    {
        return Entry.Create("tfa.initiate")
            .Describe($"{user} initiates {channel.Name} challenge")
            .From(user, 1,
                (TwoFactorTags.Role, "subject"))
            .To(TwoFactorPartyId.Channel(channel.Name), 1,
                (TwoFactorTags.Role, "channel"),
                (TwoFactorTags.Status, ChallengeStatus.Initiated))
            .Tag(TwoFactorTags.Channel, channel.Name)
            .Execute(async ctx =>
            {
                var result = await channel.InitiateAsync(user.Id, target, ctx.CancellationToken);

                if (!result.Succeeded)
                {
                    var channelParty = ctx.Operation.GetPartiesByTag(TwoFactorTags.Role, "channel").FirstOrDefault();
                    if (channelParty != null)
                    {
                        channelParty.RemoveTag(TwoFactorTags.Status);
                        channelParty.AddTag(TwoFactorTags.Status, ChallengeStatus.Failed);
                    }
                    ctx.Set("error", result.Error ?? "initiate_failed");
                    throw new InvalidOperationException(result.Error ?? "Failed to initiate challenge");
                }

                // إنشاء تحدي وحفظه
                var challenge = new Challenge
                {
                    Id = result.ChallengeId,
                    UserIdentifier = user.Id,
                    ChannelName = channel.Name,
                    Status = ChallengeStatus.Pending,
                    ExternalId = result.ProviderData?.GetValueOrDefault("externalId")
                };

                if (result.ProviderData != null)
                {
                    foreach (var (k, v) in result.ProviderData)
                        challenge.Metadata[k] = v;
                }

                if (store != null)
                    await store.SaveAsync(challenge, ctx.CancellationToken);

                ctx.Set("challenge", challenge);
                ctx.Set("challengeId", result.ChallengeId);

                var party = ctx.Operation.GetPartiesByTag(TwoFactorTags.Role, "channel").FirstOrDefault();
                if (party != null)
                {
                    party.RemoveTag(TwoFactorTags.Status);
                    party.AddTag(TwoFactorTags.Status, ChallengeStatus.Pending);
                    party.AddTag(TwoFactorTags.Challenge, result.ChallengeId);
                    if (challenge.ExternalId != null)
                        party.AddTag(TwoFactorTags.ExternalId, challenge.ExternalId);
                }
            })
            .Build();
    }

    /// <summary>
    /// قيد: التحقق من تحدي.
    /// التحدي (مدين) يُستهلك → المستخدم (دائن) يُصادق.
    /// </summary>
    public static Operation Verify(
        TwoFactorPartyId user,
        string challengeId,
        ITwoFactorChannel channel,
        string? providedCode = null,
        IChallengeStore? store = null)
    {
        var builder = Entry.Create("tfa.verify")
            .Describe($"Verify {channel.Name} challenge for {user}")
            .From(TwoFactorPartyId.Challenge(challengeId), 1,
                (TwoFactorTags.Role, "challenge"),
                (TwoFactorTags.Status, ChallengeStatus.Pending))
            .To(user, 1,
                (TwoFactorTags.Role, "subject"))
            .Tag(TwoFactorTags.Channel, channel.Name)
            .Tag(TwoFactorTags.Challenge, challengeId);

        // محلل حالة التحدي - يفحص الوجود والصلاحية والمحاولات قبل التنفيذ
        if (store != null)
            builder.Analyze(new ChallengeStateAnalyzer(store, challengeId));

        return builder
            .Execute(async ctx =>
            {
                var result = await channel.VerifyAsync(challengeId, providedCode, ctx.CancellationToken);

                var challengeParty = ctx.Operation.GetPartiesByTag(TwoFactorTags.Role, "challenge").FirstOrDefault();

                if (result.Verified)
                {
                    if (challengeParty != null)
                    {
                        challengeParty.RemoveTag(TwoFactorTags.Status);
                        challengeParty.AddTag(TwoFactorTags.Status, ChallengeStatus.Verified);
                    }

                    if (store != null)
                        await store.UpdateStatusAsync(challengeId, ChallengeStatus.Verified, ctx.CancellationToken);

                    ctx.Set("verified", true);
                }
                else
                {
                    if (challengeParty != null)
                    {
                        challengeParty.RemoveTag(TwoFactorTags.Status);
                        challengeParty.AddTag(TwoFactorTags.Status, ChallengeStatus.Failed);
                    }

                    if (store != null)
                    {
                        ctx.TryGet<Challenge>("challenge", out var existing);
                        if (existing != null)
                        {
                            existing.AttemptCount++;
                            await store.SaveAsync(existing, ctx.CancellationToken);
                        }
                    }

                    ctx.Set("verified", false);
                    ctx.Set("reason", result.Reason ?? "verification_failed");
                    throw new InvalidOperationException(result.Reason ?? "Verification failed");
                }
            })
            .Build();
    }

    /// <summary>
    /// قيد: إلغاء/انتهاء تحدي.
    /// </summary>
    public static Operation Expire(
        string challengeId,
        IChallengeStore? store = null)
    {
        return Entry.Create("tfa.expire")
            .Describe($"Expire challenge {challengeId}")
            .From(TwoFactorPartyId.Challenge(challengeId), 1,
                (TwoFactorTags.Status, ChallengeStatus.Pending))
            .To(TwoFactorPartyId.System, 1,
                (TwoFactorTags.Status, ChallengeStatus.Expired))
            .Tag(TwoFactorTags.Challenge, challengeId)
            .Execute(async ctx =>
            {
                if (store != null)
                    await store.UpdateStatusAsync(challengeId, ChallengeStatus.Expired, ctx.CancellationToken);

                ctx.Set("expiredAt", DateTimeOffset.UtcNow);
            })
            .Build();
    }
}
