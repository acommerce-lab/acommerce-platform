using System.Net.Http.Json;
using ACommerce.Chat.Client.Blazor;
using ACommerce.Chat.Operations;
using ACommerce.ClientHost.Auth;
using Microsoft.Extensions.Logging;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// عَميل دَردَشَة بَديل عَن <see cref="ChatClient"/> الافتراضيّ. يَستَهلِك
/// <see cref="AuthenticatedHttpClient"/> الذي يَحفَظ Bearer JWT في
/// <c>DefaultRequestHeaders.Authorization</c> مُباشَرَةً (يَستَمِع لـ
/// <c>IClientAuthState.OnChanged</c>). كلّ طَلَب يَخرُج بِالتَوكِن
/// الصَحيح بدون الاعتماد عَلى handler chain (الذي يَفقِد الرُؤيَة
/// في WASM).
/// </summary>
public sealed class EjarChatClient : IChatClient
{
    private readonly AuthenticatedHttpClient _http;
    private readonly ILogger<EjarChatClient> _logger;
    private readonly object _lock = new();

    public string? ActiveConversationId { get; private set; }
    public event Action<IChatMessage>? MessageReceived;

    public EjarChatClient(AuthenticatedHttpClient http, ILogger<EjarChatClient> logger)
    {
        _http = http;
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
        var resp = await _http.Client.PostAsync(path, content: null, ct);
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
            var resp = await _http.Client.PostAsync(path, content: null, ct);
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
        var resp = await _http.Client.PostAsJsonAsync(path, new { text = body }, ct);
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
