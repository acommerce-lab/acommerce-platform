using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.DataInterceptors;

namespace ACommerce.Chat.Operations;

/// <summary>
/// أنواع كيانات Chat — تطابق أسماء classes الـ entities المسجَّلة في
/// <c>EntityDiscoveryRegistry</c> عند الإقلاع. الـ <c>DataOperationHandler</c>
/// العام يبحث عن "Message" أو "MessageEntity" تلقائياً.
/// </summary>
public static class ChatEntityKinds
{
    public static readonly EntityKind Message      = new("Message");
    public static readonly EntityKind Conversation = new("Conversation");
}

/// <summary>
/// مفاتيح أوسمة Chat kit المُكتَّبة.
/// </summary>
public static class ChatTagKeys
{
    /// <summary>id المحادثة على tag <c>"conversation_id"</c>.</summary>
    public static readonly TagKey ConversationId = new("conversation_id");
}

/// <summary>
/// Markers لـ Chat — تركيبة (tag key + value) ثابتة. تستهلكها interceptors
/// خارج kit (في <c>Chat.Realtime</c>، <c>Chat.WithNotifications</c>) لتطابق
/// عبر <c>op.HasMark(ChatMarkers.IsChatMessageCreate)</c> بدل بناء tag مكرَّر.
/// </summary>
public static class ChatMarkers
{
    /// <summary>"هذه العمليّة تُنشئ رسالة دردشة" — تركيب على Crud.Create
    /// عند <c>POST /conversations/{id}/messages</c>. interceptors البثّ +
    /// إشعار + FCM يطابقون عليه (مع شرط <c>result.Success</c> صراحةً).</summary>
    public static readonly Marker IsChatMessageCreate = new("chat.action", "message.create");
}
