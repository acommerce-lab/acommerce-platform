using System.Text.Json;
using ACommerce.Chat.Client.Blazor;
using ACommerce.Chat.Operations;
using Microsoft.JSInterop;
using Order.V2.Web.Store;

namespace Order.V2.Web.Services;

/// <summary>
/// Manages the SignalR connection for this Blazor circuit. Routes incoming
/// <c>chat.message</c> payloads to <see cref="IChatClient.OnRealtimeMessage"/>
/// (filtered by the active conversation) and fires the chat-leave beacon on
/// <c>beforeunload</c>.
/// </summary>
public sealed class OrderV2RealtimeService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly AppStore _store;
    private readonly IChatClient _chat;
    private IJSObjectReference? _module;
    private DotNetObjectReference<OrderV2RealtimeService>? _self;
    private bool _connected;

    public event Action<string>? MessageReceived;
    public event Action<string>? NotificationReceived;

    public OrderV2RealtimeService(IJSRuntime js, AppStore store, IChatClient chat)
    {
        _js    = js;
        _store = store;
        _chat  = chat;
    }

    public async Task ConnectAsync(string hubUrl)
    {
        if (_connected || string.IsNullOrEmpty(_store.Auth.AccessToken)) return;
        try
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/realtime.js");
            _self ??= DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("start", hubUrl, _store.Auth.AccessToken, _self);
            await _module.InvokeVoidAsync("registerBeforeUnload", _self);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Realtime] start failed: {ex.Message}");
        }
    }

    [JSInvokable] public void OnConnected()    => _connected = true;
    [JSInvokable] public void OnReconnected()  => _connected = true;
    [JSInvokable] public void OnMessage(string json)      => MessageReceived?.Invoke(json);
    [JSInvokable] public void OnNotification(string json) => NotificationReceived?.Invoke(json);

    [JSInvokable]
    public void OnChatMessage(string json)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<ChatMessage>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (msg is not null) _chat.OnRealtimeMessage(msg);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Realtime] chat.message parse failed: {ex.Message}");
        }
    }

    [JSInvokable]
    public async Task OnBeforeUnload()
    {
        if (_chat.ActiveConversationId is { } convId && _module is not null && _store.Auth.UserId is { } userId)
        {
            var path = $"/chat/{Uri.EscapeDataString(convId)}/leave";
            var body = JsonSerializer.Serialize(new { userId });
            await _module.InvokeVoidAsync("leaveChatBeacon", path, _store.Auth.AccessToken, body);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _self?.Dispose();
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("unregisterBeforeUnload"); } catch { }
            try { await _module.InvokeVoidAsync("stop"); } catch { }
            await _module.DisposeAsync();
        }
    }
}
