using ACommerce.OperationEngine.Core;

namespace ACommerce.Compositions.Chat.Realtime;

/// <summary>
/// Operation types الصادرة من Chat kit. مُكرَّرة هنا حتى يلتقطها معترض
/// التركيب بمطابقة قيمة (record equality) بدل سلسلة.
///
/// <para>القيم تطابق ما يُولِّده ChatController في الـ Chat kit
/// (<c>"message.send"</c>) — لا نُغيِّر الـ kit، فقط نُعرِّف نسخة typed
/// محلّيّة هنا للمعترض.</para>
/// </summary>
public static class ChatOps
{
    public static readonly OperationType MessageSend = new("message.send");
}

/// <summary>مفاتيح الأوسمة التي يضعها Chat kit على عمليّاته.</summary>
public static class ChatTagKeys
{
    /// <summary>id المحادثة على tag <c>"conversation_id"</c>.</summary>
    public static readonly TagKey ConversationId = new("conversation_id");
}

/// <summary>
/// أسماء أحداث realtime التي يبثّها التركيب — متّفق عليها مع
/// realtime.js في الواجهة.
/// </summary>
public static class ChatRealtimeEvents
{
    /// <summary>الحدث الذي تستمع له الواجهة عبر <c>builder.on("chat.message", ...)</c>.</summary>
    public const string ChatMessage = "chat.message";
}
