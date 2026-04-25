using ACommerce.Chat.Client.Blazor;
using Ashare.V2.Web.Store;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Ashare.V2.Web.Services;

/// <summary>
/// Manages the SignalR connection for this Blazor circuit. Bridges incoming
/// chat.message payloads to <see cref="IChatClient"/> (which decides whether
/// to surface them — only when the user is in the matching ChatRoom).
/// Also wires the browser <c>beforeunload</c> hook so the chat client can
/// fire a leave-chat beacon before the tab closes.
/// </summary>
public sealed class Ashare2RealtimeService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly AppStore _store;
    private readonly IChatClient _chat;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Ashare2RealtimeService>? _self;
    private bool _connected;

    public event Action<string>? MessageReceived;
    public event Action<string>? NotificationReceived;

    public Ashare2RealtimeService(IJSRuntime js, AppStore store, IChatClient chat)
    {
        _js    = js;
        _store = store;
        _chat  = chat;
        _store.OnChanged += OnAuthChanged;
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

    private void OnAuthChanged()
    {
        if (!_store.Auth.IsAuthenticated && _connected)
            _ = DisconnectAsync();
    }

    [JSInvokable] public void OnConnected() => _connected = true;
    [JSInvokable] public void OnReconnected() => _connected = true;
    [JSInvokable] public void OnMessage(string json) => MessageReceived?.Invoke(json);
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
        if (_chat.ActiveConversationId is { } convId && _module is not null)
        {
            var path = $"/chat/{Uri.EscapeDataString(convId)}/leave";
            await _module.InvokeVoidAsync("leaveChatBeacon", path, _store.Auth.AccessToken);
        }
    }

    private async Task DisconnectAsync()
    {
        _connected = false;
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("unregisterBeforeUnload"); } catch { }
            await _module.InvokeVoidAsync("stop");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _store.OnChanged -= OnAuthChanged;
        _self?.Dispose();
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("unregisterBeforeUnload"); } catch { }
            try { await _module.InvokeVoidAsync("stop"); } catch { }
            await _module.DisposeAsync();
        }
    }
}
