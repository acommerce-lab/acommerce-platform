using System.Net.Http.Json;
using ACommerce.Chat.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACommerce.Chat.Client.Blazor;

/// <summary>
/// التطبيق الافتراضيّ لـ <see cref="IChatClient"/>. مستقلّ عن مزوّد الـ realtime —
/// التطبيق هو الذي ينقل رسائل الـ realtime عبر <see cref="OnRealtimeMessage"/>.
/// </summary>
public sealed class ChatClient : IChatClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ChatClientOptions _opts;
    private readonly ILogger<ChatClient> _logger;
    private readonly object _lock = new();

    public string? ActiveConversationId { get; private set; }
    public event Action<IChatMessage>? MessageReceived;

    public ChatClient(IHttpClientFactory httpFactory, IOptions<ChatClientOptions> opts, ILogger<ChatClient> logger)
    {
        _httpFactory = httpFactory;
        _opts        = opts.Value;
        _logger      = logger;
    }

    private HttpClient Http => _opts.HttpClientName is { } name
        ? _httpFactory.CreateClient(name)
        : _httpFactory.CreateClient();

    public async Task EnterAsync(string conversationId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(conversationId))
            throw new ArgumentException("conversationId required", nameof(conversationId));

        // If we're already in a different conversation, leave it first.
        string? prior;
        lock (_lock) { prior = ActiveConversationId; }
        if (prior is not null && prior != conversationId)
            await LeaveAsync(ct);

        var path = _opts.EnterPathTemplate.Replace("{convId}", Uri.EscapeDataString(conversationId));
        var resp = await Http.PostAsync(path, content: null, ct);
        resp.EnsureSuccessStatusCode();

        lock (_lock) { ActiveConversationId = conversationId; }
        _logger.LogDebug("[ChatClient] entered conv={Conv}", conversationId);
    }

    public async Task LeaveAsync(CancellationToken ct = default)
    {
        string? convId;
        lock (_lock) { convId = ActiveConversationId; ActiveConversationId = null; }
        if (convId is null) return;

        try
        {
            var path = _opts.LeavePathTemplate.Replace("{convId}", Uri.EscapeDataString(convId));
            var resp = await Http.PostAsync(path, content: null, ct);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ChatClient] leave call failed for conv={Conv}; backend will time out the channel", convId);
        }
    }

    public async Task SendAsync(string body, CancellationToken ct = default)
    {
        string? convId;
        lock (_lock) { convId = ActiveConversationId; }
        if (convId is null)
            throw new InvalidOperationException("No active conversation. Call EnterAsync first.");

        var path = _opts.SendPathTemplate.Replace("{convId}", Uri.EscapeDataString(convId));
        var resp = await Http.PostAsJsonAsync(path, new { body }, ct);
        resp.EnsureSuccessStatusCode();
    }

    public void OnRealtimeMessage(IChatMessage message)
    {
        string? active;
        lock (_lock) { active = ActiveConversationId; }

        if (message.ConversationId == active)
            MessageReceived?.Invoke(message);
        // else: notification channel handles it; this client is silent for other conversations.
    }
}
