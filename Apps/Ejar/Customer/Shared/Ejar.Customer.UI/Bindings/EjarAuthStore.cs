using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.Kits.Auth.Frontend.Customer.Stores;
using Ejar.Customer.UI.Interceptors;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IAuthStore"/> لإيجار. يَستخدم HttpClient مباشرة (عبر
/// <see cref="EjarCircuitHttp"/> فيَصل للـ JWT بَعْد التَحقّق). لا يَعتمد
/// على envelope parsing فلا يَنهار لو الـ remote شَكَّل ردّاً غير قياسيّ.
///
/// <para>سياسة التَسامح:
///   <list type="bullet">
///     <item>POST /auth/otp/request: HTTP 2xx ⇒ نَجاح، أيّ شيء آخر ⇒ خطأ.</item>
///     <item>POST /auth/otp/verify: نَستخرج token + userId + name من JSON
///           بأكثر من نمط (envelope.data.* أو data.* أو root.*).</item>
///   </list>
/// </para>
/// </summary>
public sealed class EjarAuthStore : IAuthStore, IDisposable
{
    private readonly AppStore _app;
    private readonly EjarCircuitHttp _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public EjarAuthStore(AppStore app, EjarCircuitHttp http)
    {
        _app = app;
        _http = http;
        _app.OnChanged += FireChanged;
    }

    public bool IsAuthenticated => _app.Auth.IsAuthenticated;
    public string? UserId      => _app.Auth.UserId?.ToString();
    public string? FullName    => _app.Auth.FullName;
    public bool IsBusy { get; private set; }
    public string? LastError { get; private set; }
    public event Action? Changed;

    public async Task RequestOtpAsync(string phone, CancellationToken ct = default)
    {
        IsBusy = true; LastError = null; FireChanged();
        try
        {
            var resp = await _http.Client.PostAsJsonAsync(
                "/auth/otp/request", new { phone }, _json, ct);
            if (!resp.IsSuccessStatusCode)
                LastError = $"otp_request_failed ({(int)resp.StatusCode})";
            // أيّ 2xx ⇒ نَجاح. الـ widget يَنتقل لـ Code step تلقائياً.
        }
        catch (Exception ex) { LastError = $"network_error: {ex.Message}"; }
        finally { IsBusy = false; FireChanged(); }
    }

    public async Task VerifyOtpAsync(string phone, string code, CancellationToken ct = default)
    {
        IsBusy = true; LastError = null; FireChanged();
        try
        {
            var resp = await _http.Client.PostAsJsonAsync(
                "/auth/otp/verify", new { phone, code }, _json, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"otp_verify_failed ({(int)resp.StatusCode})";
                return;
            }

            // نَبحث عن { token, userId, name } في أيّ مَوقع: data، root، أو envelope.data
            using var doc = JsonDocument.Parse(raw);
            var (token, userId, name) = ExtractAuth(doc.RootElement);
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
            {
                LastError = "otp_verify_no_token";
                return;
            }

            _app.Auth.UserId      = Guid.TryParse(userId, out var g) ? g : null;
            _app.Auth.FullName    = name ?? "—";
            _app.Auth.Phone       = phone;
            _app.Auth.AccessToken = token;
            _app.NotifyChanged();
        }
        catch (Exception ex) { LastError = $"network_error: {ex.Message}"; }
        finally { IsBusy = false; FireChanged(); }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        IsBusy = true; FireChanged();
        try
        {
            try { await _http.Client.PostAsync("/auth/logout", null, ct); } catch { }
            _app.Auth.UserId      = null;
            _app.Auth.FullName    = null;
            _app.Auth.Phone       = null;
            _app.Auth.AccessToken = null;
            _app.NotifyChanged();
        }
        finally { IsBusy = false; FireChanged(); }
    }

    private static (string? Token, string? UserId, string? Name) ExtractAuth(JsonElement root)
    {
        // 1) envelope.data.{token,userId,name}
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            var t = Pick(data, "token", "accessToken");
            var u = Pick(data, "userId", "id");
            var n = Pick(data, "name", "fullName", "displayName");
            if (t is not null && u is not null) return (t, u, n);
        }
        // 2) root.{token,userId,name}
        if (root.ValueKind == JsonValueKind.Object)
        {
            var t = Pick(root, "token", "accessToken");
            var u = Pick(root, "userId", "id");
            var n = Pick(root, "name", "fullName", "displayName");
            if (t is not null && u is not null) return (t, u, n);
        }
        return (null, null, null);
    }

    private static string? Pick(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
            if (obj.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        return null;
    }

    private void FireChanged() => Changed?.Invoke();
    public void Dispose() => _app.OnChanged -= FireChanged;
}
