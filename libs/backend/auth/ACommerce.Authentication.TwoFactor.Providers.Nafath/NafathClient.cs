using ACommerce.Authentication.TwoFactor.Providers.Nafath.Options;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace ACommerce.Authentication.TwoFactor.Providers.Nafath;

/// <summary>
/// واجهة عميل Nafath - للتبديل بين HTTP وmockup.
/// </summary>
public interface INafathClient
{
    /// <summary>إطلاق طلب تحقق لهوية (رقم وطني)</summary>
    Task<NafathInitiateResponse> InitiateAsync(string nationalId, CancellationToken ct = default);

    /// <summary>استعلام عن حالة طلب</summary>
    Task<NafathStatusResponse> GetStatusAsync(string transactionId, CancellationToken ct = default);
}

public record NafathInitiateResponse(
    bool Success,
    string? TransactionId,
    string? VerificationCode,
    string? Error);

public record NafathStatusResponse(
    bool Success,
    string Status,          // "PENDING", "COMPLETED", "REJECTED", "EXPIRED"
    string? NationalId,
    string? Error);

/// <summary>
/// عميل Nafath عبر HTTP.
/// </summary>
public class HttpNafathClient : INafathClient
{
    private readonly HttpClient _http;
    private readonly NafathOptions _options;
    private readonly ILogger<HttpNafathClient> _logger;

    public HttpNafathClient(HttpClient http, NafathOptions options, ILogger<HttpNafathClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NafathInitiateResponse> InitiateAsync(string nationalId, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                nationalId,
                service = _options.ApplicationName,
                locale = "ar"
            };

            var body = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/api/v1/mfa/request")
            {
                Content = body
            };
            AddAuthHeaders(request);

            var response = await _http.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Nafath] Initiate failed: {Status} - {Body}", response.StatusCode, raw);
                return new NafathInitiateResponse(false, null, null, $"HTTP {(int)response.StatusCode}: {raw}");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var transId = root.TryGetProperty("transId", out var t) ? t.GetString() : null;
            var code = root.TryGetProperty("random", out var c) ? c.GetString() : null;

            return new NafathInitiateResponse(true, transId, code, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Nafath] Initiate exception");
            return new NafathInitiateResponse(false, null, null, ex.Message);
        }
    }

    public async Task<NafathStatusResponse> GetStatusAsync(string transactionId, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_options.BaseUrl}/api/v1/mfa/request/status?transId={Uri.EscapeDataString(transactionId)}");
            AddAuthHeaders(request);

            var response = await _http.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return new NafathStatusResponse(false, "ERROR", null, $"HTTP {(int)response.StatusCode}: {raw}");

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "UNKNOWN" : "UNKNOWN";
            var natId = root.TryGetProperty("nationalId", out var n) ? n.GetString() : null;

            return new NafathStatusResponse(true, status.ToUpperInvariant(), natId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Nafath] GetStatus exception");
            return new NafathStatusResponse(false, "ERROR", null, ex.Message);
        }
    }

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.TryAddWithoutValidation("Authorization", $"Basic {credentials}");
    }
}
