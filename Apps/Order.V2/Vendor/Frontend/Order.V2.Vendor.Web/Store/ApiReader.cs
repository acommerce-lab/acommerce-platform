using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.OperationEngine.Wire;
using Order.V2.Vendor.Web.Interceptors;

namespace Order.V2.Vendor.Web.Store;

public class ApiReader
{
    private readonly VendorCircuitHttp _circuit;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiReader(VendorCircuitHttp circuit) { _circuit = circuit; }

    public async Task<OperationEnvelope<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        try
        {
            var resp = await _circuit.Client.GetAsync(path, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty");
        }
        catch (Exception ex) { return OperationEnvelopeFactory.Error<T>("network_error", ex.Message); }
    }

    public async Task<OperationEnvelope<T>> PostAsync<T>(string path, object payload, CancellationToken ct = default)
    {
        try
        {
            var resp = await _circuit.Client.PostAsJsonAsync(path, payload, _json, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty");
        }
        catch (Exception ex) { return OperationEnvelopeFactory.Error<T>("network_error", ex.Message); }
    }

    public async Task<OperationEnvelope<T>> PutAsync<T>(string path, object payload, CancellationToken ct = default)
    {
        try
        {
            var content = JsonContent.Create(payload, options: _json);
            var resp = await _circuit.Client.PutAsync(path, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty");
        }
        catch (Exception ex) { return OperationEnvelopeFactory.Error<T>("network_error", ex.Message); }
    }

    public async Task<OperationEnvelope<T>> DeleteAsync<T>(string path, CancellationToken ct = default)
    {
        try
        {
            var resp = await _circuit.Client.DeleteAsync(path, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty");
        }
        catch (Exception ex) { return OperationEnvelopeFactory.Error<T>("network_error", ex.Message); }
    }
}
