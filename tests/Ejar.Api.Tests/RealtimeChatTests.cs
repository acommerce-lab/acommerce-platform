using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.Realtime.Operations.Abstractions;
using Ejar.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ejar.Api.Tests;

/// <summary>
/// Proof-of-life لـ chat realtime داخل العمليّة. الهدف: محاكاة سيناريو المُستخدم
/// (طرفان على نفس المحادثة، أحدهما يُرسل، الآخر يستلم) في بيئة معزولة بدون
/// IIS/runasp.net، لنتأكّد هل المنطق سليم على مستوى الكود أم لا.
/// لو المرور هنا = الخلل في الإنتاج خارج كود الكيت/الـ DI (IIS، تخزين، إلخ).
/// لو الفشل هنا = الخلل في الكود ونرى السبب.
/// </summary>
public class RealtimeChatTests : IClassFixture<EjarFactory>
{
    private readonly EjarFactory _factory;
    public RealtimeChatTests(EjarFactory factory) => _factory = factory;

    [Fact(Timeout = 30_000)]
    public async Task Both_parties_receive_chat_message_via_realtime()
    {
        var http = _factory.CreateClient();

        // 1) سجّل دخول طرفَين عبر OTP. الـ Auth Kit dev mock يقبل الرمز "123456".
        var (tokenA, userIdA) = await LoginAsync(http, "771111111");
        var (tokenB, userIdB) = await LoginAsync(http, "772222222");
        Assert.NotEqual(userIdA, userIdB);

        // 2) فعّل اشتراك للمستخدم A لكي يستطيع نشر إعلان.
        await ActivatePlanAsync(http, tokenA);

        // 3) A ينشر إعلاناً.
        var listingId = await CreateListingAsync(http, tokenA);

        // 4) B يفتح محادثة على الإعلان (B = Owner، A = Partner = listing owner).
        var convId = await StartConversationAsync(http, tokenB, listingId, "مرحباً");

        // 5) كلا الطرفَين يتّصلان عبر SignalR على /realtime ضمن TestServer.
        var connA = await ConnectRealtimeAsync(tokenA);
        var connB = await ConnectRealtimeAsync(tokenB);

        var atA = new TaskCompletionSource<JsonElement>();
        var atB = new TaskCompletionSource<JsonElement>();
        connA.On<JsonElement>("chat.message", msg =>
        {
            if (msg.TryGetProperty("body", out var b) && b.GetString() == "ping-from-B")
                atA.TrySetResult(msg);
        });
        connB.On<JsonElement>("chat.message", msg =>
        {
            if (msg.TryGetProperty("body", out var b) && b.GetString() == "ping-from-A")
                atB.TrySetResult(msg);
        });

        // 6) كلاهما يدخل غرفة الدردشة.
        await PostAsync(http, tokenA, $"/chat/{convId}/enter");
        await PostAsync(http, tokenB, $"/chat/{convId}/enter");

        // 7) B → A: A يجب أن يستلم.
        await PostJsonAsync(http, tokenB, $"/conversations/{convId}/messages",
            new { text = "ping-from-B" });
        var winA = await Task.WhenAny(atA.Task, Task.Delay(5_000));
        Assert.True(winA == atA.Task, "A لم يستلم رسالة B عبر realtime.");

        // 8) A → B: B يجب أن يستلم.
        await PostJsonAsync(http, tokenA, $"/conversations/{convId}/messages",
            new { text = "ping-from-A" });
        var winB = await Task.WhenAny(atB.Task, Task.Delay(5_000));
        Assert.True(winB == atB.Task, "B لم يستلم رسالة A عبر realtime.");

        await connA.DisposeAsync();
        await connB.DisposeAsync();
    }

    [Fact(Timeout = 30_000)]
    public async Task Recipient_gets_persistent_notification_in_db()
    {
        // اختبار التزامن مع جدول الإشعارات: أيّ رسالة تُكتَب → سطر إشعار للطرف الآخر.
        var http = _factory.CreateClient();
        var (tokenA, _) = await LoginAsync(http, "773333333");
        var (tokenB, userIdB) = await LoginAsync(http, "774444444");
        await ActivatePlanAsync(http, tokenA);
        var listingId = await CreateListingAsync(http, tokenA);
        var convId    = await StartConversationAsync(http, tokenB, listingId, "بدء");

        await PostAsync(http, tokenB, $"/chat/{convId}/enter");
        await PostJsonAsync(http, tokenA, $"/conversations/{convId}/messages",
            new { text = "تنبيه" });

        // امهل ٢٠٠ms للسطر يكتمل.
        await Task.Delay(200);

        var resp = await GetAsync(http, tokenB, "/notifications");
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.NotEmpty(rows);
        Assert.Contains(rows, r =>
            r.GetProperty("type").GetString() == "chat.message" &&
            r.GetProperty("body").GetString()!.Contains("تنبيه"));
    }

    // ───────────────────────── helpers ─────────────────────────

    private async Task<(string token, string userId)> LoginAsync(HttpClient http, string phone)
    {
        var req  = await PostJsonAsync(http, null, "/auth/otp/request", new { phone });
        Assert.True(req.IsSuccessStatusCode, await req.Content.ReadAsStringAsync());

        var ver  = await PostJsonAsync(http, null, "/auth/otp/verify",
            new { phone, code = "123456" });
        Assert.True(ver.IsSuccessStatusCode, await ver.Content.ReadAsStringAsync());

        var body = await ver.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        return (data.GetProperty("token").GetString()!,
                data.GetProperty("userId").GetString()!);
    }

    private async Task ActivatePlanAsync(HttpClient http, string token)
    {
        // اقرأ أوّل خطّة من /plans (anon) ثمّ فعّلها للمستخدم.
        var plansResp = await http.GetAsync("/plans");
        var plansJson = await plansResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(plansJson);
        var first = doc.RootElement.GetProperty("data").EnumerateArray().First();
        var planId = first.GetProperty("id").GetString()!;

        var resp = await PostJsonAsync(http, token, "/subscriptions/activate",
            new { planId });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
    }

    private async Task<string> CreateListingAsync(HttpClient http, string token)
    {
        var resp = await PostJsonAsync(http, token, "/my-listings", new
        {
            title = "اختبار realtime",
            description = "اختبار",
            price = 1000m,
            timeUnit = "monthly",
            propertyType = "apartment",
            city = "صنعاء",
            district = "الحي",
            lat = 0.0,
            lng = 0.0,
            bedroomCount = 1,
        });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty("id").GetString()!;
    }

    private async Task<string> StartConversationAsync(
        HttpClient http, string token, string listingId, string firstText)
    {
        var resp = await PostJsonAsync(http, token, "/conversations/start",
            new { listingId, text = firstText });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty("id").GetString()!;
    }

    private async Task<HubConnection> ConnectRealtimeAsync(string token)
    {
        var conn = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/realtime"), opts =>
            {
                opts.AccessTokenProvider = () => Task.FromResult<string?>(token);
                opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                opts.Transports =
                    Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();
        await conn.StartAsync();
        // امهل الباك ٢٠٠ms ليُشغّل OnConnectedAsync ويُسجّل في الـ tracker.
        await Task.Delay(200);
        return conn;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string? token, string path)
        => PostJsonAsync(http, token, path, new { });

    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient http, string? token, string path, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, path);
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(body);
        return await http.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient http, string token, string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(req);
    }
}

/// <summary>
/// Test factory: SQLite ملف مؤقّت بدل MSSQL، اقفل الـ logging الصاخب،
/// وفّر <c>Program</c> ليُعرَف لـ WebApplicationFactory.
/// </summary>
public sealed class EjarFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(),
        $"ejar-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "sqlite");
        builder.UseSetting("Database:ConnectionString", $"Data Source={_dbPath}");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }
}
