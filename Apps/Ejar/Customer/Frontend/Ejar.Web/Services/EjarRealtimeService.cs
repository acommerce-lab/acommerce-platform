using Microsoft.JSInterop;
using Ejar.Web.Store;

namespace Ejar.Web.Services;

/// <summary>
/// Manages the SignalR connection for this Blazor circuit.
/// Connect() is called from the layout after authentication is confirmed.
/// Exposes events that components can subscribe to for live updates.
/// </summary>
public sealed class EjarRealtimeService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly AppStore _store;
    private IJSObjectReference? _module;
    private DotNetObjectReference<EjarRealtimeService>? _self;
    private bool _connected;

    public event Action<string>? MessageReceived;
    public event Action<string>? NotificationReceived;

    public EjarRealtimeService(IJSRuntime js, AppStore store)
    {
        _js    = js;
        _store = store;
        _store.OnChanged += OnAuthChanged;
    }

    public async Task ConnectAsync(string hubUrl)
    {
        if (_connected || string.IsNullOrEmpty(_store.Auth.AccessToken)) return;
        try
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>(
                "import", "./js/realtime.js");
            _self ??= DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("start", hubUrl, _store.Auth.AccessToken, _self);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Realtime] start failed: {ex.Message}");
        }
    }

    private void OnAuthChanged()
    {
        // Disconnect when logged out
        if (!_store.Auth.IsAuthenticated && _connected)
            _ = DisconnectAsync();
    }

    [JSInvokable]
    public void OnConnected() => _connected = true;

    [JSInvokable]
    public void OnReconnected() => _connected = true;

    [JSInvokable]
    public void OnMessage(string json) => MessageReceived?.Invoke(json);

    [JSInvokable]
    public void OnNotification(string json) => NotificationReceived?.Invoke(json);

    private async Task DisconnectAsync()
    {
        _connected = false;
        if (_module is not null)
            await _module.InvokeVoidAsync("stop");
    }

    public async ValueTask DisposeAsync()
    {
        _store.OnChanged -= OnAuthChanged;
        _self?.Dispose();
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("stop"); } catch { }
            await _module.DisposeAsync();
        }
    }
}
