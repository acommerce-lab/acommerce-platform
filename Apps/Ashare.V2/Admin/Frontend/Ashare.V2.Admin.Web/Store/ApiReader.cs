using System.Text.Json;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Admin.Web.Interceptors;

namespace Ashare.V2.Admin.Web.Store;

public class ApiReader
{
    private readonly AdminCircuitHttp _circuit;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiReader(AdminCircuitHttp circuit) { _circuit = circuit; }

    public async Task<OperationEnvelope<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        try
        {
            var resp = await _circuit.Client.GetAsync(path, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty envelope");
        }
        catch (Exception ex) { return OperationEnvelopeFactory.Error<T>("network_error", ex.Message); }
    }
}
