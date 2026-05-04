using ACommerce.Compositions.Core;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Notifications.Backend;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Compositions.Chat.WithNotifications;

/// <summary>
/// Bundle: عند نجاح <c>message.send</c>، يُرسل عمليّة <b>بنت</b>
/// <c>notification.create</c> عبر <see cref="INotificationDispatcher"/>.
///
/// <para>الـ interceptor <b>لا يلمس</b> <c>INotificationStore</c> ولا DB
/// إطلاقاً. تتدفّق الرسالة في OAM بطريقة محاسبيّة سليمة:</para>
/// <list type="number">
///   <item><c>message.send</c> ينجح (Chat kit).</item>
///   <item>Post-interceptor هنا يبني وصفاً للإشعار.</item>
///   <item>يُمرّره لـ <c>INotificationDispatcher</c> الذي يفتح
///         <c>notification.create</c> envelope جديداً (parent =
///         <c>message.send</c>) فيمرّ بكامل دورة OAM للإشعار:
///         analyzers + interceptors + SaveAtEnd.</item>
/// </list>
///
/// <para>الفائدة: عمليّة الإشعار صارت حدثاً مرئيّاً للـ audit log،
/// قابلاً للعكس، يمرّ بكلّ interceptor مسجَّل على
/// <c>notification.create</c> (مثلاً rate-limit، spam-detection،
/// quiet-hours)، ولا تتسرّب يد كيت الدردشة إلى DB الإشعارات.</para>
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
        // ملاحظة عن نجاح العمليّة: مرحلة Post في OpEngine تعمل فقط حين تنجح
        // Execute + AfterExecute hooks (بما فيها SaveAtEnd). لو أيّهما رمى،
        // الكود يقفز لـ catch وينتقل لـ Error hooks لا Post-analyzers
        // (راجع OperationEngine.ExecuteAsync). إذاً مجرّد أنّ هذا الكود
        // يجري = الرسالة حُفظت ذرّيّاً. لا حاجة لفحص result.Success
        // (والـ adapter يُمرّر result=null دوماً، فالفحص كان دائماً يكسر التدفّق).
        try
        {
            using var scope = _root.CreateScope();
            var dispatcher = scope.ServiceProvider.GetService<INotificationDispatcher>();
            var chatStore  = scope.ServiceProvider.GetService<IChatStore>();
            if (dispatcher is null || chatStore is null) return AnalyzerResult.Pass();

            var convTag = ctx.Operation.Tags.FirstOrDefault(t => t.Key == "conversation_id");
            var convId = string.IsNullOrEmpty(convTag.Key) ? null : convTag.Value;
            if (string.IsNullOrEmpty(convId)) return AnalyzerResult.Pass();

            // ChatController.Send يَكتب From(party, 1, ("role","sender")) — لا
            // direction=debit. نَدعم الاثنَين فلا يَفشل التَركيب على شَكلَين.
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
            if (string.IsNullOrEmpty(recipientId)) return AnalyzerResult.Pass();

            // F4: presence-aware — لو المستلم حاضر في هذه المحادثة الآن
            // (ChatRoom مفتوحة + /enter مستدعى)، لا نُنشئ سجلّ إشعار.
            // الرسالة تظهر له مباشرةً عبر realtime — الإشعار مكرَّر مزعج.
            var probe = scope.ServiceProvider.GetService<ACommerce.Kits.Chat.Backend.IPresenceProbe>();
            if (probe is not null && await probe.IsUserActiveInConversationAsync(recipientId, convId, ctx.CancellationToken))
            {
                _log.LogDebug("Chat.PersistentNotification: مستلم {Rid} حاضر في {Conv} — تجاوز", recipientId, convId);
                return AnalyzerResult.Pass();
            }

            // الجسم: نأخذ النصّ من ctx.Entity<IChatMessage>() لو أتى عبر F1،
            // وإلاّ نقبل tag(text) إن وُجد.
            var msg = ctx.Entity<ACommerce.Chat.Operations.IChatMessage>();
            var bodyTag = ctx.Operation.Tags.FirstOrDefault(t => t.Key == "text");
            var body = msg?.Body ?? (string.IsNullOrEmpty(bodyTag.Key) ? "رسالة جديدة" : bodyTag.Value);
            var preview = body.Length > 80 ? body[..80] + "…" : body;

            // الفعل المحاسبيّ: عمليّة بنت notification.create. لا نلمس
            // INotificationStore هنا — الـ dispatcher يبني envelope كامل
            // مع analyzers + SaveAtEnd. parent = message.send op.
            await dispatcher.DispatchCreateAsync(
                userId:    recipientId,
                type:      "chat.message",
                title:     "رسالة جديدة",
                body:      preview,
                relatedId: convId,
                parent:    ctx.Operation,
                ct:        ctx.CancellationToken);
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
