using ACommerce.OperationEngine.Core;
namespace ACommerce.Realtime.Operations.Abstractions;

/// <summary>
/// مفاتيح العلامات المعيارية لمكتبة الزمن الحقيقي.
/// هذه ليست ثوابت enum - المفاتيح ثابتة لكن القيم حرة.
///
/// مثال:
///   party.AddTag(RT.Group, "chat_123")      ← القيمة من التطبيق
///   party.AddTag(RT.Delivery, "pending")    ← القيمة من حالة العملية
///   op.AddTag(RT.Transport, "signalr")      ← القيمة من المزود
///   op.AddTag(RT.Method, "ReceiveMessage")  ← القيمة من البروتوكول
///
/// العلامات ≠ ثوابت. المفاتيح معيارية، القيم تأتي من العالم الخارجي.
/// </summary>
public static class RT
{
    // === مفاتيح على العملية ===

    /// <summary>
    /// نوع النقل المستخدم. القيم: "signalr", "websocket", "grpc", etc.
    /// </summary>
    public static readonly TagKey Transport = new("transport");

    /// <summary>
    /// اسم الدالة/الطريقة المُستدعاة. القيم: "ReceiveMessage", "UserTyping", etc.
    /// </summary>
    public static readonly TagKey Method = new("method");

    // === مفاتيح على الأطراف ===

    /// <summary>
    /// المجموعة/الغرفة. القيم: "chat_123", "topic_news", "payment_456"
    /// </summary>
    public static readonly TagKey Group = new("group");

    /// <summary>
    /// معرف الاتصال. القيمة: connectionId من المزود
    /// </summary>
    public static readonly TagKey ConnectionId = new("connection_id");

    /// <summary>
    /// حالة التسليم. القيم: "pending", "sent", "delivered", "read", "failed"
    /// </summary>
    public static readonly TagKey Delivery = new("delivery");

    /// <summary>
    /// حالة الحضور. القيم: "online", "offline", "away", "busy"
    /// </summary>
    public static readonly TagKey Presence = new("presence");

    /// <summary>
    /// دور الطرف. القيم: "sender", "recipient", "observer"
    /// </summary>
    public static readonly TagKey Role = new("role");
}
