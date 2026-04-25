using System.Text.Json;
using ACommerce.OperationEngine.Wire;
using Ejar.Provider.Web.Interceptors;

namespace Ejar.Provider.Web.Store;

public class ApiReader
{
    private readonly HttpClient _http;
    private readonly CultureInterceptor _culture;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiReader(HttpClient http, CultureInterceptor culture)
    {
        _http = http;
        _culture = culture;
    }

    public async Task<OperationEnvelope<T>> GetAsync<T>(
        string path,
        bool localize = false,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync(path, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            var env = JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                     ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty envelope");
            await _culture.LocalizeAsync(env, forced: localize);
            return env;
        }
        catch (Exception ex)
        {
            return OperationEnvelopeFactory.Error<T>("network_error", ex.Message);
        }
    }

    public async Task<OperationEnvelope<T>> PostAsync<T>(
        string path,
        object? body = null,
        CancellationToken ct = default)
    {
        try
        {
            var content = body is null
                ? null
                : new StringContent(JsonSerializer.Serialize(body, _json),
                                    System.Text.Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(path, content, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(raw, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty envelope");
        }
        catch (Exception ex)
        {
            return OperationEnvelopeFactory.Error<T>("network_error", ex.Message);
        }
    }

    public async Task<OperationEnvelope<T>> PutAsync<T>(
        string path,
        object? body = null,
        CancellationToken ct = default)
    {
        try
        {
            var content = body is null
                ? null
                : new StringContent(JsonSerializer.Serialize(body, _json),
                                    System.Text.Encoding.UTF8, "application/json");
            var resp = await _http.PutAsync(path, content, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(raw, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty envelope");
        }
        catch (Exception ex)
        {
            return OperationEnvelopeFactory.Error<T>("network_error", ex.Message);
        }
    }

    public async Task<OperationEnvelope<T>> DeleteAsync<T>(
        string path,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync(path, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<OperationEnvelope<T>>(raw, _json)
                   ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty envelope");
        }
        catch (Exception ex)
        {
            return OperationEnvelopeFactory.Error<T>("network_error", ex.Message);
        }
    }
}
