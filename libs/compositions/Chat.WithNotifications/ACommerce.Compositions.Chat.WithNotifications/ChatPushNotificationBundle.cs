using ACommerce.Compositions.Core;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Notification.Operations.Abstractions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Compositions.Chat.WithNotifications;

/// <summary>
/// Bundle: عند نجاح <c>message.send</c>، أرسل إشعار push (FCM/Web Push)
/// للمستلم عبر <see cref="INotificationChannel"/>. لو لم يُسجَّل قناة (التطبيق
/// بلا FCM)، التركيب لا يفعل شيئاً — Chat kit نفسه لا يعرف بـ Push.
/// </summary>
public sealed class ChatPushNotificationBundle : IInterceptorBundle
{
    public string Name => "Chat.WithNotifications.Push";
    public IEnumerable<Type> InterceptorTypes => new[] { typeof(ChatPushNotificationInterceptor) };
}

public sealed class ChatPushNotificationInterceptor : IOperationInterceptor
{
    private readonly IServiceProvider _root;
    private readonly ILogger<ChatPushNotificationInterceptor> _log;

    public string Name => "Chat.PushNotification";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public ChatPushNotificationInterceptor(
        IServiceProvider root, ILogger<ChatPushNotificationInterceptor> log)
    { _root = root; _log = log; }

    public bool AppliesTo(Operation op) => op.Type == "message.send";

    public async Task<AnalyzerResult> InterceptAsync(OperationContext ctx, OperationResult? result = null)
    {
        // Post-phase يعمل فقط لو نجح Execute + AfterExecute (بما فيه SaveAtEnd)؛
        // الـ adapter يُمرّر result=null دوماً، فلا نقرأها.
        try
        {
            using var scope = _root.CreateScope();
            var pushChannel = scope.ServiceProvider.GetService<INotificationChannel>();
            if (pushChannel is null) return AnalyzerResult.Pass(); // FCM غير مكوَّن

            var chatStore = scope.ServiceProvider.GetService<IChatStore>();
            if (chatStore is null) return AnalyzerResult.Pass();

            var convTag = ctx.Operation.Tags.FirstOrDefault(t => t.Key == "conversation_id");
            var convId = string.IsNullOrEmpty(convTag.Key) ? null : convTag.Value;
            if (string.IsNullOrEmpty(convId)) return AnalyzerResult.Pass();

            // ChatController.Send يَكتب From(CallerPartyId, 1, ("role", "sender"))
            // — لا direction=debit. الكود السابق كان يَبحث عن debit ⇒ sender
            // = null ⇒ Pass() ⇒ صفر FCM دفعات. هذا سَبب جوهريّ لعدم وصول
            // الإشعارات رغم صحّة الـ Firebase creds + التَوكنات.
            var sender = ctx.Operation.Parties
                .FirstOrDefault(p => p.Tags.Any(t =>
                    (t.Key == "role"      && t.Value == "sender") ||
                    (t.Key == "direction" && t.Value == "debit")));
            if (sender is null) return AnalyzerResult.Pass();
            var senderId = ExtractId(sender.Identity);

            var conv = await chatStore.GetConversationAsync(convId, ctx.CancellationToken);
            if (conv is null) return AnalyzerResult.Pass();

            string? recipientId = null;
            foreach (var pid in conv.ParticipantPartyIds)
            {
                var rid = ExtractId(pid);
                if (rid != senderId) { recipientId = rid; break; }
            }
            if (string.IsNullOrEmpty(recipientId) || recipientId == senderId)
                return AnalyzerResult.Pass();

            // F4: presence-aware — مستلم حاضر في المحادثة لا يحتاج FCM
            // (الرسالة تصل مباشرةً عبر SignalR). إشعار النظام مزعج وفجّ.
            var probe = scope.ServiceProvider.GetService<ACommerce.Kits.Chat.Backend.IPresenceProbe>();
            if (probe is not null && await probe.IsUserActiveInConversationAsync(recipientId, convId, ctx.CancellationToken))
            {
                _log.LogDebug("Chat.PushNotification: مستلم {Rid} حاضر في {Conv} — تجاوز", recipientId, convId);
                return AnalyzerResult.Pass();
            }

            var msg = ctx.Entity<ACommerce.Chat.Operations.IChatMessage>();
            var bodyTag = ctx.Operation.Tags.FirstOrDefault(t => t.Key == "text");
            var body = msg?.Body ?? (string.IsNullOrEmpty(bodyTag.Key) ? "رسالة جديدة" : bodyTag.Value);
            var preview = body.Length > 80 ? body[..80] + "…" : body;

            await pushChannel.SendAsync(
                userId:  recipientId,
                title:   "رسالة جديدة",
                message: preview,
                data:    new { conversationId = convId, type = "chat.message" },
                ct:      ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Chat.PushNotification: فشل غير قاتل");
        }
        return AnalyzerResult.Pass();
    }

    private static string ExtractId(string identity)
    {
        var idx = identity.IndexOf(':');
        return idx >= 0 ? identity[(idx + 1)..] : identity;
    }
}
