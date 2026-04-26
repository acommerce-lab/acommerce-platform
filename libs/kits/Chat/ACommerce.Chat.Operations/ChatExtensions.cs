using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Chat.Operations;

public static class ChatExtensions
{
    /// <summary>
    /// يسجّل <see cref="IChatService"/> + التطبيق الافتراضيّ <see cref="ChatService"/>.
    /// شرط: <see cref="IRealtimeChannelManager"/> و <see cref="IRealtimeTransport"/>
    /// مسجَّلان (يحدث تلقائيّاً عند استعمال أيّ مزوّد realtime).
    /// </summary>
    public static IServiceCollection AddChat(this IServiceCollection services)
    {
        services.AddSingleton<IChatService, ChatService>();
        return services;
    }

    /// <summary>
    /// يربط السلوك القياسيّ: عند فتح <c>chat:conv:X</c> لمستخدم → يُغلَق
    /// <c>notif:conv:X</c> له. عند إغلاق <c>chat:conv:X</c> (أيًّا كان السبب —
    /// idle / explicit / disconnect) → يُعاد فتح <c>notif:conv:X</c> له.
    ///
    /// <para>نعرّفها هنا لأنّها سياسة قابلة للنقاش — تطبيقات أخرى قد ترغب
    /// بسلوك مختلف (مثلاً: لا تعيد الإشعارات بعد disconnect لأنّ المستخدم
    /// انقطع عن الإنترنت أصلاً). ربط مرّة واحدة في <c>Program.cs</c>:
    /// <code>app.Services.WireChatNotificationCoupling();</code></para>
    /// </summary>
    public static IServiceProvider WireChatNotificationCoupling(this IServiceProvider sp)
    {
        var channels = sp.GetRequiredService<IRealtimeChannelManager>();

        // chat opens → close notif for same conv
        channels.OnChannelOpened(ChatChannels.ChatPrefix + "*", async ev =>
        {
            var convId = ChatChannels.ConversationIdOf(ev.ChannelId);
            if (convId is null) return;
            await channels.CloseAsync(ev.UserId, ChatChannels.Notif(convId));
        });

        // chat closes → re-open notif for same conv
        channels.OnChannelClosed(ChatChannels.ChatPrefix + "*", async ev =>
        {
            var convId = ChatChannels.ConversationIdOf(ev.ChannelId);
            if (convId is null) return;
            // We need a connection id for the notif channel to know who to re-add.
            // Disconnect close has none — in that case the user is gone, no need to re-open.
            if (string.IsNullOrEmpty(ev.ConnectionId)) return;
            await channels.OpenAsync(ev.UserId, ev.ConnectionId, ChatChannels.Notif(convId));
        });

        return sp;
    }
}
