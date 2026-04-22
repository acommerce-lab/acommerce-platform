using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.OperationEngine.Wire;

namespace Ashare.V2.Provider.Web.Store;

public class ApiReader
{
    private readonly AppStore _store;
    private readonly IHttpClientFactory _factory;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiReader(IHttpClientFactory factory, AppStore store)
    {
        _factory = factory;
        _store   = store;
    }

    private HttpClient CreateClient()
    {
        var http = _factory.CreateClient("ashare-v2");
        if (_store.Auth.IsAuthenticated)
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _store.Auth.AccessToken);
        else
            http.DefaultRequestHeaders.Authorization = null;
        return http;
    }

    public async Task<OperationEnvelope<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        try
        {
            var http = CreateClient();
            var resp = await http.GetAsync(path, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] != '{')
                return OperationEnvelopeFactory.Error<T>($"http_{(int)resp.StatusCode}", "");
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty");
        }
        catch (Exception ex) { return OperationEnvelopeFactory.Error<T>("network_error", ex.Message); }
    }

    public async Task<OperationEnvelope<T>> PostAsync<T>(string path, object payload, CancellationToken ct = default)
    {
        try
        {
            var http = CreateClient();
            var resp = await http.PostAsJsonAsync(path, payload, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] != '{')
                return OperationEnvelopeFactory.Error<T>($"http_{(int)resp.StatusCode}", "");
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty");
        }
        catch (Exception ex) { return OperationEnvelopeFactory.Error<T>("network_error", ex.Message); }
    }
}
