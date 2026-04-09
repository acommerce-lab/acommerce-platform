using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.OperationEngine.Wire;

namespace Order.Web2.Services;

/// <summary>
/// Lightweight typed client for Order.Api2. Every backend call is wrapped
/// in <c>OperationEnvelope&lt;T&gt;</c> so the UI can read both the data and
/// the underlying accounting operation status uniformly.
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

    private void Auth()
    {
        if (_auth.IsAuthenticated)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _auth.AccessToken);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<OperationEnvelope<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        Auth();
        var resp = await _http.GetAsync(path, ct);
        var env = await resp.Content.ReadFromJsonAsync<OperationEnvelope<T>>(_json, ct);
        return env ?? new OperationEnvelope<T> { Error = new OperationError { Code = "empty_response" } };
    }

    public async Task<OperationEnvelope<T>> PostAsync<T>(string path, object payload, CancellationToken ct = default)
    {
        Auth();
        var resp = await _http.PostAsJsonAsync(path, payload, _json, ct);
        var env = await resp.Content.ReadFromJsonAsync<OperationEnvelope<T>>(_json, ct);
        return env ?? new OperationEnvelope<T> { Error = new OperationError { Code = "empty_response" } };
    }

    public async Task<OperationEnvelope<T>> PutAsync<T>(string path, object payload, CancellationToken ct = default)
    {
        Auth();
        var resp = await _http.PutAsJsonAsync(path, payload, _json, ct);
        var env = await resp.Content.ReadFromJsonAsync<OperationEnvelope<T>>(_json, ct);
        return env ?? new OperationEnvelope<T> { Error = new OperationError { Code = "empty_response" } };
    }
}
