using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using Microsoft.Extensions.Logging;

namespace ACommerce.Authentication.TwoFactor.Providers.Nafath;

/// <summary>
/// قناة 2FA عبر Nafath (نفاذ - الهوية الوطنية السعودية).
/// لا تُولّد كوداً داخلياً - الكود يُعرض على المستخدم من Nafath App.
/// التحقق يتم بالاستعلام عن حالة المعاملة لدى Nafath.
/// </summary>
public class NafathTwoFactorChannel : ITwoFactorChannel
{
    private readonly INafathClient _client;
    private readonly ILogger<NafathTwoFactorChannel> _logger;

    public string Name => "nafath";

    /// <summary>
    /// Nafath لا تُولد كوداً نستخدمه - الكود يُعرض على المستخدم فقط.
    /// التحقق يتم بالاستعلام عن حالة المعاملة.
    /// </summary>
    public bool GeneratesCode => false;

    public NafathTwoFactorChannel(INafathClient client, ILogger<NafathTwoFactorChannel> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChallengeResult> InitiateAsync(
        string userIdentifier,
        string? target = null,
        CancellationToken ct = default)
    {
        // userIdentifier هنا هو الرقم الوطني
        var nationalId = target ?? userIdentifier;

        if (string.IsNullOrWhiteSpace(nationalId))
            return new ChallengeResult(false, "", "national_id_required");

        _logger.LogInformation("[Nafath] Initiating challenge for {NationalId}", MaskId(nationalId));

        var response = await _client.InitiateAsync(nationalId, ct);

        if (!response.Success || string.IsNullOrEmpty(response.TransactionId))
            return new ChallengeResult(false, "", response.Error ?? "initiate_failed");

        var providerData = new Dictionary<string, string>
        {
            ["externalId"] = response.TransactionId,
            ["nationalId"] = nationalId
        };

        if (!string.IsNullOrEmpty(response.VerificationCode))
        {
            // هذا الكود يُعرض على المستخدم في واجهة التطبيق ليتطابق مع ما يظهر في Nafath App
            providerData["displayCode"] = response.VerificationCode;
        }

        _logger.LogInformation("[Nafath] Challenge initiated, transId: {TransId}", response.TransactionId);

        return new ChallengeResult(
            Succeeded: true,
            ChallengeId: response.TransactionId,
            ProviderData: providerData);
    }

    public async Task<VerificationResult> VerifyAsync(
        string challengeId,
        string? providedCode = null,
        CancellationToken ct = default)
    {
        // لا نحتاج providedCode - فقط نستعلم عن حالة المعاملة
        var response = await _client.GetStatusAsync(challengeId, ct);

        if (!response.Success)
            return new VerificationResult(false, response.Error ?? "status_check_failed");

        _logger.LogDebug("[Nafath] Status for {TransId}: {Status}", challengeId, response.Status);

        return response.Status switch
        {
            "COMPLETED" => new VerificationResult(true, null,
                new Dictionary<string, string>
                {
                    ["nationalId"] = response.NationalId ?? "",
                    ["status"] = "COMPLETED"
                }),

            "PENDING" => new VerificationResult(false, "pending",
                new Dictionary<string, string> { ["status"] = "PENDING" }),

            "REJECTED" => new VerificationResult(false, "rejected",
                new Dictionary<string, string> { ["status"] = "REJECTED" }),

            "EXPIRED" => new VerificationResult(false, "expired",
                new Dictionary<string, string> { ["status"] = "EXPIRED" }),

            _ => new VerificationResult(false, $"unknown_status:{response.Status}")
        };
    }

    private static string MaskId(string id) =>
        id.Length > 4 ? $"****{id[^4..]}" : "****";
}
