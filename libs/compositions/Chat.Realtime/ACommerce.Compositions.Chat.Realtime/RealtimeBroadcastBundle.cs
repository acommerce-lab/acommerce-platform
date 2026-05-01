using ACommerce.Chat.Operations;
using ACommerce.Compositions.Core;
using ACommerce.Kits.Chat.Backend;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Compositions.Chat.Realtime;

/// <summary>
/// Bundle: عند نجاح <see cref="ChatOps.MessageSend"/>، ابثّ الرسالة لـ
/// المرسِل والمستلم عبر <see cref="IRealtimeTransport"/>. هذا يُنشئ "user
/// pin" مباشر بصرف النظر عن عضويّة الـ groups (مكمِّل لبثّ Chat kit
/// على chat:conv:X).
///
/// <para>سابقاً كان هذا منطق بداخل <c>EjarCustomerChatStore.AppendMessageAsync</c>.
/// نقلناه إلى bundle خارجيّ ليصبح Chat kit نقيّاً (لا يعرف Realtime) ويصبح
/// التركيب قابلاً للتركيب فوقه.</para>
/// </summary>
public sealed class RealtimeBroadcastBundle : IInterceptorBundle
{
    public string Name => "Chat.Realtime.Broadcast";
    public IEnumerable<Type> InterceptorTypes => new[] { typeof(RealtimeBroadcastInterceptor) };
}

public sealed class RealtimeBroadcastInterceptor : IOperationInterceptor
{
    private readonly IServiceProvider _root;
    private ILogger<RealtimeBroadcastInterceptor>? _log;

    public string Name => "Chat.RealtimeBroadcast";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    // DI ctor — يستلم IServiceProvider (Singleton-safe، root sp).
    // Scoped services (IChatStore) تُحلّ عبر CreateScope() لكلّ استدعاء.
    public RealtimeBroadcastInterceptor(IServiceProvider root) { _root = root; }

    public bool AppliesTo(Operation op) => op.Type == ChatOps.MessageSend.Name;

    public async Task<AnalyzerResult> InterceptAsync(OperationContext ctx, OperationResult? result = null)
    {
        // F5: لا بثّ على عمليّة فاشلة. نجاح Execute body شرط ضروريّ —
        // فلا نُرسل رسالة لم تُحفظ في DB.
        if (result is null || !result.Success) return AnalyzerResult.Pass();

        try
        {
            // scope per-call ليصل لـ Scoped services (IChatStore, DbContext).
            using var scope = _root.CreateScope();
            var transport = scope.ServiceProvider.GetService<IRealtimeTransport>();
            if (transport is null) return AnalyzerResult.Pass();
            var chatStore = scope.ServiceProvider.GetService<IChatStore>();

            // ① conversationId من الوسم. Tag struct فلا يُستعمل '?'؛ استخدم ?:
            var convTag = ctx.Operation.Tags
                .FirstOrDefault(t => t.Key == ChatTagKeys.ConversationId.Name);
            var convId = string.IsNullOrEmpty(convTag.Key) ? null : convTag.Value;
            if (string.IsNullOrEmpty(convId)) return AnalyzerResult.Pass();

            // ② sender من From party (direction=debit). نَحلّ "Kind:Id" → Id.
            var sender = ctx.Operation.Parties
                .FirstOrDefault(p => p.Tags.Any(t => t.Key == "direction" && t.Value == "debit"));
            if (sender is null) return AnalyzerResult.Pass();
            var senderId = ExtractId(sender.Identity);

            // ③ recipient: ParticipantPartyIds (مجموعة "Kind:Id") — نتجاوز
            // المرسل لنحصل على الآخر. أبسط شكل interface (Law 6).
            string? recipientId = null;
            if (chatStore is not null)
            {
                var conv = await chatStore.GetConversationAsync(convId, ctx.CancellationToken);
                if (conv is not null)
                {
                    foreach (var partyId in conv.ParticipantPartyIds)
                    {
                        var rid = ExtractId(partyId);
                        if (rid != senderId) { recipientId = rid; break; }
                    }
                }
            }

            // ④ payload: shape مبسّط. المعترض post-execute فالرسالة محفوظة
            // والواجهة ستجلبها عبر GetMessagesAsync — هنا ping للتحديث.
            object payload = new { conversationId = convId, sentAt = DateTime.UtcNow };

            await transport.SendToUserAsync(
                senderId, ChatRealtimeEvents.ChatMessage, payload, ctx.CancellationToken);

            if (!string.IsNullOrEmpty(recipientId) && recipientId != senderId)
            {
                await transport.SendToUserAsync(
                    recipientId, ChatRealtimeEvents.ChatMessage, payload, ctx.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Chat.RealtimeBroadcast: غير قاتل — رسالة محفوظة لكن البثّ فشل");
        }
        return AnalyzerResult.Pass();
    }

    private static string ExtractId(string identity)
    {
        // "User:abc-123" → "abc-123". لو ما فيها ":" فهي raw id.
        var idx = identity.IndexOf(':');
        return idx >= 0 ? identity[(idx + 1)..] : identity;
    }
}
