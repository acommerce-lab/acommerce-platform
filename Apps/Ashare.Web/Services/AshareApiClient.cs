using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.OperationEngine.Wire;

namespace Ashare.Web.Services;

/// <summary>
/// عميل HTTP بسيط يرسل/يستقبل OperationEnvelope&lt;T&gt; من Ashare.Api.
///
/// في تطبيق حقيقي كبير، سنستخدم ClientOpEngine + HttpDispatcher + Domain libs.
/// هنا نستخدم wrapper أبسط لتقصير الوقت - كل الطرق تُرجع OperationEnvelope
/// للحفاظ على نفس نمط البيانات المحاسبي عبر الـ wire.
/// </summary>
public class AshareApiClient
{
    private readonly HttpClient _http;
    private readonly AuthStateService _auth;
    private readonly JsonSerializerOptions _jsonOptions;

    public AshareApiClient(HttpClient http, AuthStateService auth)
    {
        _http = http;
        _auth = auth;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private void SetAuth()
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

        _http.DefaultRequestHeaders.AcceptLanguage.Clear();
        _http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(_auth.Language));
    }

    public async Task<OperationEnvelope<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        SetAuth();
        var response = await _http.GetAsync(path, ct);
        var envelope = await response.Content.ReadFromJsonAsync<OperationEnvelope<T>>(_jsonOptions, ct);
        return envelope ?? new OperationEnvelope<T>
        {
            Error = new OperationError { Code = "empty_response" }
        };
    }

    public async Task<OperationEnvelope<T>> PostAsync<T>(string path, object payload, CancellationToken ct = default)
    {
        SetAuth();
        var response = await _http.PostAsJsonAsync(path, payload, _jsonOptions, ct);
        var envelope = await response.Content.ReadFromJsonAsync<OperationEnvelope<T>>(_jsonOptions, ct);
        return envelope ?? new OperationEnvelope<T>
        {
            Error = new OperationError { Code = "empty_response" }
        };
    }

    public async Task<OperationEnvelope<T>> DeleteAsync<T>(string path, CancellationToken ct = default)
    {
        SetAuth();
        var response = await _http.DeleteAsync(path, ct);
        var envelope = await response.Content.ReadFromJsonAsync<OperationEnvelope<T>>(_jsonOptions, ct);
        return envelope ?? new OperationEnvelope<T>
        {
            Error = new OperationError { Code = "empty_response" }
        };
    }
}
