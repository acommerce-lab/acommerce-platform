using System.Net.Http.Json;
using ACommerce.Chat.Client.Blazor;
using ACommerce.Chat.Operations;
using Ejar.Customer.UI.Interceptors;
using Microsoft.Extensions.Logging;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// عميل دردشة بديل عن <see cref="ChatClient"/> الافتراضيّ. السبب: الافتراضيّ
/// يطلب <see cref="HttpClient"/> من <see cref="IHttpClientFactory"/> عبر
/// <c>CreateClient("ejar")</c> فيعتمد على <c>AuthHeadersHandler</c> الموجود
/// في handler chain. هذا الـ handler يُحقَن من DI scope الخاصّ بـ
/// <see cref="IHttpClientFactory"/> الذي قد لا يرى الـ <see cref="AppStore"/>
/// الذي تمّ فيه حفظ الـ JWT بعد تسجيل الدخول. النتيجة: الطلبات بلا Bearer
/// header → <c>POST /chat/{id}/enter → 401</c> → <c>SendAsync</c> يفشل بـ
/// "No active conversation. Call EnterAsync first." لأنّ EnterAsync ابتلع
/// الفشل صامتاً ولم يُحدّث <c>ActiveConversationId</c>.
///
/// <para><b>الحلّ هنا:</b> نمرّ عبر <see cref="EjarCircuitHttp.Client"/> الذي
/// يحفظ التوكن في <c>DefaultRequestHeaders.Authorization</c> مباشرةً (يستمع
/// لتغييرات <see cref="Store.AppStore.OnChanged"/>). كلّ طلب يخرج بالتوكن
/// الصحيح بدون الاعتماد على الـ handler chain.</para>
/// </summary>
public sealed class EjarChatClient : IChatClient
{
    private readonly EjarCircuitHttp _circuit;
    private readonly ILogger<EjarChatClient> _logger;
    private readonly object _lock = new();

    public string? ActiveConversationId { get; private set; }
    public event Action<IChatMessage>? MessageReceived;

    public EjarChatClient(EjarCircuitHttp circuit, ILogger<EjarChatClient> logger)
    {
        _circuit = circuit;
        _logger  = logger;
    }

    public async Task EnterAsync(string conversationId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(conversationId))
            throw new ArgumentException("conversationId required", nameof(conversationId));

        string? prior;
        lock (_lock) { prior = ActiveConversationId; }
        if (prior is not null && prior != conversationId)
            await LeaveAsync(ct);

        var path = $"/chat/{Uri.EscapeDataString(conversationId)}/enter";
        var resp = await _circuit.Client.PostAsync(path, content: null, ct);
        resp.EnsureSuccessStatusCode();

        lock (_lock) { ActiveConversationId = conversationId; }
        _logger.LogDebug("[EjarChat] entered conv={Conv}", conversationId);
    }

    public async Task LeaveAsync(CancellationToken ct = default)
    {
        string? convId;
        lock (_lock) { convId = ActiveConversationId; ActiveConversationId = null; }
        if (convId is null) return;

        try
        {
            var path = $"/chat/{Uri.EscapeDataString(convId)}/leave";
            var resp = await _circuit.Client.PostAsync(path, content: null, ct);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EjarChat] leave failed for conv={Conv}", convId);
        }
    }

    public async Task SendAsync(string body, CancellationToken ct = default)
    {
        string? convId;
        lock (_lock) { convId = ActiveConversationId; }
        if (convId is null)
            throw new InvalidOperationException("No active conversation. Call EnterAsync first.");

        var path = $"/conversations/{Uri.EscapeDataString(convId)}/messages";
        var resp = await _circuit.Client.PostAsJsonAsync(path, new { text = body }, ct);
        resp.EnsureSuccessStatusCode();
    }

    public void OnRealtimeMessage(IChatMessage message)
    {
        string? active;
        lock (_lock) { active = ActiveConversationId; }
        if (message.ConversationId == active)
            MessageReceived?.Invoke(message);
    }
}
