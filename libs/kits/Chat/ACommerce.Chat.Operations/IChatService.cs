using ACommerce.Realtime.Operations.Abstractions;

namespace ACommerce.Chat.Operations;

/// <summary>
/// واجهة خدمة الدردشة التي يستهلكها التطبيق:
/// <list type="number">
///   <item>عند دخول مستخدم محادثة → <see cref="EnterConversationAsync"/> يفتح <c>chat:conv:X</c> له بمهلة خمول، ويغلق <c>notif:conv:X</c> عبر hook الـ realtime.</item>
///   <item>عند مغادرة المستخدم → <see cref="LeaveConversationAsync"/> يغلق <c>chat:conv:X</c> ويعيد فتح <c>notif:conv:X</c>.</item>
///   <item>عند إضافة مشترِك جديد للمحادثة → <see cref="SubscribeUserAsync"/> يشترك بـ <c>notif:conv:X</c> بدون انتهاء مدّة.</item>
///   <item>عند إرسال رسالة جديدة → <see cref="BroadcastNewMessageAsync"/> يبثّ على القناتين معاً؛ المستلِم يصله ما هو مشترِك فيه.</item>
/// </list>
///
/// التطبيق يربط منطقه عبر <see cref="IRealtimeChannelManager.OnChannelOpened"/> /
/// <see cref="IRealtimeChannelManager.OnChannelClosed"/> — لا يُستدعى من داخل
/// هذه المكتبة. <see cref="ChatExtensions.WireChatNotificationCoupling"/>
/// يربطها لك بسطر واحد إذا أردت السلوك القياسيّ.
/// </summary>
public interface IChatService
{
    Task EnterConversationAsync(
        string conversationId,
        string userId,
        string connectionId,
        TimeSpan? idleTimeout = null,
        CancellationToken ct = default);

    Task LeaveConversationAsync(
        string conversationId,
        string userId,
        CancellationToken ct = default);

    Task SubscribeUserAsync(
        string conversationId,
        string userId,
        string connectionId,
        CancellationToken ct = default);

    Task UnsubscribeUserAsync(
        string conversationId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// يبثّ الرسالة الجديدة على قناة الدردشة وقناة الإشعارات معاً
    /// (المعرّف يأخذ من <see cref="IChatMessage.ConversationId"/>). كلّ مستلِم
    /// يستقبل ما هو مشترِك فيه فقط — وهذا يضمن: من فتح الدردشة يرى الرسالة
    /// داخلها، ومن لم يفتحها يصله إشعار.
    /// </summary>
    Task BroadcastNewMessageAsync(IChatMessage message, CancellationToken ct = default);
}
