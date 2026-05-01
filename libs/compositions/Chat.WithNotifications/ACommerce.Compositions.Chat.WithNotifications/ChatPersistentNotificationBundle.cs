using ACommerce.Compositions.Core;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Notifications.Backend;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Compositions.Chat.WithNotifications;

/// <summary>
/// Bundle: عند نجاح <c>message.send</c>، أنشئ سجلّ إشعار في الـ inbox
/// للمستلم عبر <see cref="INotificationStore.CreateAsync"/>. لا Chat kit
/// ولا Notifications kit يعرف الآخر — التركيب الخارجيّ يربطهما.
/// </summary>
public sealed class ChatPersistentNotificationBundle : IInterceptorBundle
{
    public string Name => "Chat.WithNotifications.PersistentRecord";
    public IEnumerable<Type> InterceptorTypes => new[] { typeof(ChatPersistentNotificationInterceptor) };
}

public sealed class ChatPersistentNotificationInterceptor : IOperationInterceptor
{
    private readonly IServiceProvider _root;
    private readonly ILogger<ChatPersistentNotificationInterceptor> _log;

    public string Name => "Chat.PersistentNotification";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public ChatPersistentNotificationInterceptor(
        IServiceProvider root, ILogger<ChatPersistentNotificationInterceptor> log)
    { _root = root; _log = log; }

    public bool AppliesTo(Operation op) => op.Type == "message.send";

    public async Task<AnalyzerResult> InterceptAsync(OperationContext ctx, OperationResult? result = null)
    {
        try
        {
            using var scope = _root.CreateScope();
            var notifStore = scope.ServiceProvider.GetService<INotificationStore>();
            var chatStore  = scope.ServiceProvider.GetService<IChatStore>();
            if (notifStore is null || chatStore is null) return AnalyzerResult.Pass();

            var convTag = ctx.Operation.Tags.FirstOrDefault(t => t.Key == "conversation_id");
            var convId = string.IsNullOrEmpty(convTag.Key) ? null : convTag.Value;
            if (string.IsNullOrEmpty(convId)) return AnalyzerResult.Pass();

            var sender = ctx.Operation.Parties
                .FirstOrDefault(p => p.Tags.Any(t => t.Key == "direction" && t.Value == "debit"));
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
            if (string.IsNullOrEmpty(recipientId)) return AnalyzerResult.Pass();

            // الجسم: نأخذ النصّ من ctx.Entity<IChatMessage>() لو أتى عبر F1،
            // وإلاّ نقبل tag(text) إن وُجد. الـ subject من Conversation.
            var msg = ctx.Entity<ACommerce.Chat.Operations.IChatMessage>();
            var bodyTag = ctx.Operation.Tags.FirstOrDefault(t => t.Key == "text");
            var body = msg?.Body ?? (string.IsNullOrEmpty(bodyTag.Key) ? "رسالة جديدة" : bodyTag.Value);
            var preview = body.Length > 80 ? body[..80] + "…" : body;
            var subject = conv is ACommerce.Chat.Operations.IChatConversation c
                ? c.Id : "محادثة";

            await notifStore.CreateAsync(
                userId: recipientId,
                type:   "chat.message",
                title:  "رسالة جديدة",
                body:   preview,
                relatedId: convId,
                ct: ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Chat.PersistentNotification: غير قاتل");
        }
        return AnalyzerResult.Pass();
    }

    private static string ExtractId(string identity)
    {
        var idx = identity.IndexOf(':');
        return idx >= 0 ? identity[(idx + 1)..] : identity;
    }
}
