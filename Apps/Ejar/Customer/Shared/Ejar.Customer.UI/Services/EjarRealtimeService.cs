using ACommerce.Chat.Client.Blazor;
using ACommerce.Chat.Operations;
using Ejar.Customer.UI.Store;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// Manages the SignalR connection for this Blazor circuit. Routes incoming
/// realtime payloads to the right consumer:
/// <list type="bullet">
///   <item><c>chat.message</c> → <see cref="IChatClient.OnRealtimeMessage"/> (filtered to active conversation).</item>
///   <item><c>ReceiveMessage</c>, <c>ReceiveNotification</c> → public events for non-chat consumers.</item>
/// </list>
/// Also wires the browser's <c>beforeunload</c> event so the chat client can
/// fire a leave-chat beacon before the tab closes — guaranteeing the backend
/// re-opens the user's notification channel for that conversation.
/// </summary>
public sealed class EjarRealtimeService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly AppStore _store;
    private readonly IChatClient _chat;
    private IJSObjectReference? _module;
    private DotNetObjectReference<EjarRealtimeService>? _self;
    private bool _connected;

    public event Action<string>? MessageReceived;
    public event Action<string>? NotificationReceived;

    /// <summary>
    /// هل القناة الحاليّة قائمة. يستعمله <c>MainLayout</c> ليقرّر إعادة
    /// الاتّصال بعد logout/login على نفس الـ tab — العَلَم المحلّي عنده
    /// يبقى true بعد logout بينما هذا يعكس حالة الـ JS connection فعلاً.
    /// </summary>
    public bool IsConnected => _connected;

    public EjarRealtimeService(IJSRuntime js, AppStore store, IChatClient chat)
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
            _module ??= await _js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Ejar.Customer.UI/js/realtime.js");
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

    [JSInvokable] public void OnConnected()    => _connected = true;
    [JSInvokable] public void OnReconnected()  => _connected = true;
    [JSInvokable] public void OnMessage(string json)      => MessageReceived?.Invoke(json);
    [JSInvokable] public void OnNotification(string json) => NotificationReceived?.Invoke(json);

    /// <summary>Bridges chat.message payloads to the chat client.</summary>
    [JSInvokable]
    public async Task OnChatMessage(string json)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<ChatMessage>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (msg is null) return;
            _chat.OnRealtimeMessage(msg);

            // إذا المستخدم خارج المحادثة المعنيّة (في صفحة أخرى أو محادثة
            // أخرى)، أظهر toast إشعار نظام التشغيل + نغمة تنبيه. هذه هي
            // الإشعارات داخل التطبيق التي طلبها المستخدم — تصل دون أن يحتاج
            // المستلم فتح المحادثة. خارج التطبيق تماماً (تبويب مغلق): يأتي
            // FCM إن كان مكوَّناً.
            if (_chat.ActiveConversationId != msg.ConversationId)
            {
                var preview = string.IsNullOrEmpty(msg.Body) ? "رسالة جديدة"
                            : (msg.Body.Length > 80 ? msg.Body[..80] + "…" : msg.Body);
                try
                {
                    await _js.InvokeVoidAsync("ejarNotify.show",
                        "رسالة جديدة",
                        preview,
                        new {
                            url = $"/chat/{msg.ConversationId}",
                            tag = $"chat:{msg.ConversationId}",
                            alwaysShow = true,
                            renotify = true
                        });
                }
                catch { /* غير قاتل */ }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Realtime] chat.message parse failed: {ex.Message}");
        }
    }

    /// <summary>Browser is closing — fire the chat-leave beacon if a conversation is open.</summary>
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
