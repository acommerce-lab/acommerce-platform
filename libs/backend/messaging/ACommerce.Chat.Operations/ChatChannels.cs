namespace ACommerce.Chat.Operations;

/// <summary>
/// أسماء القنوات المعياريّة لكلّ محادثة. القاعدة:
/// <list type="bullet">
///   <item><c>chat:conv:{id}</c> — قناة دردشة المحادثة. مهلة خمول. يفتحها المستخدم عند دخوله الصفحة.</item>
///   <item><c>notif:conv:{id}</c> — قناة إشعارات المحادثة. دائمة. يشترك بها المستخدم تلقائيّاً ويغادرها عند فتح الدردشة.</item>
/// </list>
///
/// كلا القناتين <b>لكلّ محادثة لا لكلّ مستخدم</b>. الفرق في طبقة الاشتراك:
/// المستخدم نفسه ينتقل من <c>notif</c> إلى <c>chat</c> لقناة المحادثة <c>X</c> فقط،
/// مع بقائه مشترِكاً في <c>notif</c> لباقي محادثاته.
/// </summary>
public static class ChatChannels
{
    public const string ChatPrefix  = "chat:conv:";
    public const string NotifPrefix = "notif:conv:";

    public static string Chat(string conversationId)  => $"{ChatPrefix}{conversationId}";
    public static string Notif(string conversationId) => $"{NotifPrefix}{conversationId}";

    /// <summary>هل المعرّف ينتمي إلى نطاق قناة دردشة محادثة؟</summary>
    public static bool IsChatChannel(string channelId)  => channelId.StartsWith(ChatPrefix);
    /// <summary>هل المعرّف ينتمي إلى نطاق قناة إشعار محادثة؟</summary>
    public static bool IsNotifChannel(string channelId) => channelId.StartsWith(NotifPrefix);

    /// <summary>يستخرج <c>conversationId</c> من معرّف قناة معروف، أو <c>null</c>.</summary>
    public static string? ConversationIdOf(string channelId)
    {
        if (channelId.StartsWith(ChatPrefix))  return channelId[ChatPrefix.Length..];
        if (channelId.StartsWith(NotifPrefix)) return channelId[NotifPrefix.Length..];
        return null;
    }
}
