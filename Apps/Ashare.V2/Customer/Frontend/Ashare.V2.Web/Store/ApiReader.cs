using System.Text.Json;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Web.Interceptors;

namespace Ashare.V2.Web.Store;

/// <summary>
/// قارئ GET ل Ashare.V2 API. يفكّ OperationEnvelope.
/// بعد فكّ الـ envelope نمرّره على <see cref="TimezoneLocalizer"/>: إن حمل
/// الوسم <c>localize_times</c> من الخادم، أو طلب المنادي <c>localizeTimes=true</c>،
/// تُحوَّل كلّ حقول DateTime إلى توقيت المتصفّح قبل وصولها للصفحة.
/// </summary>
public class ApiReader
{
    private readonly HttpClient _http;
    private readonly TimezoneLocalizer _localizer;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiReader(HttpClient http, TimezoneLocalizer localizer)
    {
        _http = http;
        _localizer = localizer;
    }

    public async Task<OperationEnvelope<T>> GetAsync<T>(
        string path,
        bool localizeTimes = false,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync(path, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            var env = JsonSerializer.Deserialize<OperationEnvelope<T>>(body, _json)
                     ?? OperationEnvelopeFactory.Error<T>("parse_error", "empty envelope");
            await _localizer.LocalizeAsync(env, forced: localizeTimes);
            return env;
        }
        catch (Exception ex)
        {
            return OperationEnvelopeFactory.Error<T>("network_error", ex.Message);
        }
    }
}
