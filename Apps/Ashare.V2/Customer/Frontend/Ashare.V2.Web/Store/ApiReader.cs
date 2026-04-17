using System.Text.Json;
using ACommerce.OperationEngine.Wire;

namespace Ashare.V2.Web.Store;

/// <summary>
/// قارئ GET ل Ashare.V2 API. يفكّ OperationEnvelope.
/// </summary>
public class ApiReader
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiReader(HttpClient http)
    {
        _http = http;
    }

    public async Task<OperationEnvelope<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync(path, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            var env = JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json);
            return env ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty envelope");
        }
        catch (Exception ex)
        {
            return OperationEnvelopeFactory.Error<T>("network_error", ex.Message);
        }
    }
}
