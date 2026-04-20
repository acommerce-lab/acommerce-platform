using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.OperationEngine.Wire;

namespace Order.V2.Web.Store;

public class ApiReader
{
    private readonly HttpClient _http;
    private readonly AppStore _store;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiReader(HttpClient http, AppStore store)
    {
        _http = http;
        _store = store;
    }

    public async Task<OperationEnvelope<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        if (_store.Auth.IsAuthenticated)
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _store.Auth.AccessToken);
        else
            _http.DefaultRequestHeaders.Authorization = null;

        try
        {
            var resp = await _http.GetAsync(path, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] != '{')
                return new OperationEnvelope<T> { Error = new OperationError { Code = $"http_{(int)resp.StatusCode}" } };

            return JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                   ?? new OperationEnvelope<T> { Error = new OperationError { Code = "empty" } };
        }
        catch (Exception ex)
        {
            return new OperationEnvelope<T> { Error = new OperationError { Code = "network_error", Message = ex.Message } };
        }
    }
}
