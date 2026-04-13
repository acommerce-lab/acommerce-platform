using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.OperationEngine.Wire;

namespace Order.Web.Services;

/// <summary>
/// Typed HTTP client for Order.Api. Every call returns
/// <c>OperationEnvelope&lt;T&gt;</c>. Non-JSON responses (404, 500, empty)
/// are caught and turned into an error envelope so callers never see
/// a raw JsonException.
/// </summary>
public class OrderApiClient
{
    private readonly HttpClient _http;
    private readonly AuthStateService _auth;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public OrderApiClient(HttpClient http, AuthStateService auth)
    {
        _http = http;
        _auth = auth;
    }

    private void SetAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _auth.IsAuthenticated
            ? new AuthenticationHeaderValue("Bearer", _auth.AccessToken)
            : null;
    }

    public Task<OperationEnvelope<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        SetAuth();
        return SafeReadAsync<T>(() => _http.GetAsync(path, ct), ct);
    }

    public Task<OperationEnvelope<T>> PostAsync<T>(string path, object payload, CancellationToken ct = default)
    {
        SetAuth();
        return SafeReadAsync<T>(() => _http.PostAsJsonAsync(path, payload, _json, ct), ct);
    }

    public Task<OperationEnvelope<T>> PutAsync<T>(string path, object payload, CancellationToken ct = default)
    {
        SetAuth();
        return SafeReadAsync<T>(() => _http.PutAsJsonAsync(path, payload, _json, ct), ct);
    }

    public Task<OperationEnvelope<T>> DeleteAsync<T>(string path, CancellationToken ct = default)
    {
        SetAuth();
        return SafeReadAsync<T>(() => _http.DeleteAsync(path, ct), ct);
    }

    private async Task<OperationEnvelope<T>> SafeReadAsync<T>(
        Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try { resp = await send(); }
        catch (Exception ex)
        {
            return ErrorEnvelope<T>("network_error", ex.Message);
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] != '{')
        {
            return ErrorEnvelope<T>(
                $"http_{(int)resp.StatusCode}",
                $"Non-JSON response ({resp.StatusCode}): {body?[..Math.Min(body?.Length ?? 0, 200)]}");
        }

        try
        {
            var env = JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json);
            return env ?? ErrorEnvelope<T>("empty_response", "Envelope was null");
        }
        catch (JsonException ex)
        {
            return ErrorEnvelope<T>("json_parse_error", ex.Message);
        }
    }

    private static OperationEnvelope<T> ErrorEnvelope<T>(string code, string? message = null) => new()
    {
        Error = new OperationError { Code = code, Message = message }
    };
}
