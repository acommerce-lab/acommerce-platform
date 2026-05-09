using ACommerce.ClientHost.KitApi;

namespace ACommerce.Kits.Auth.Frontend.Customer.Stores;

/// <summary>
/// تنفيذ افتراضيّ يَستهلك <see cref="KitHttpClient"/>. كلّ analyzers
/// (مثل RequiredAuthAnalyzer لو سُجِّل) لا يَنطبق على endpoints الـ auth
/// لأنّها قبل تَوَفّر JWT — interceptors تَنطبق (telemetry).
/// </summary>
public sealed class HttpAuthApiClient : IAuthApiClient
{
    private const string Kit = "auth";
    private readonly KitHttpClient _http;

    public HttpAuthApiClient(KitHttpClient http) => _http = http;

    public async Task<AuthRequestResult> RequestOtpAsync(string phone, CancellationToken ct = default)
    {
        var res = await _http.PostAsync<OtpRequestDto>(Kit, "/auth/otp/request", new { phone }, ct);
        return new AuthRequestResult(res.Success, res.Error);
    }

    public async Task<AuthVerifyResult> VerifyOtpAsync(string phone, string code, CancellationToken ct = default)
    {
        var res = await _http.PostAsync<OtpVerifyDto>(Kit, "/auth/otp/verify", new { phone, code }, ct);
        if (!res.Success || res.Data is null)
            return new AuthVerifyResult(false, null, null, null, null, null, res.Error ?? "verify_failed");
        var d = res.Data;
        if (string.IsNullOrEmpty(d.Token) || string.IsNullOrEmpty(d.UserId))
            return new AuthVerifyResult(false, null, null, null, null, null, "verify_no_token");
        return new AuthVerifyResult(true, d.Token, d.UserId, d.Name, d.Phone, d.Role, null);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        await _http.PostAsync<object>(Kit, "/auth/logout", null, ct);
    }

    private sealed record OtpRequestDto(string? Masked, int? ExpiresInSeconds);
    private sealed record OtpVerifyDto(
        string? Token,
        string? UserId,
        string? Name,
        string? Phone,
        string? Role);
}
