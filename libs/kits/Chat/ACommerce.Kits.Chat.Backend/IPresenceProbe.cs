namespace ACommerce.Kits.Chat.Backend;

/// <summary>
/// عقد بسيط: هل المستخدم "حاضر" داخل محادثة معيّنة الآن؟ — أي فتح
/// ChatRoom + استدعى <c>POST /chat/{conv}/enter</c> ولم يخرج.
///
/// <para>يُستهلَك من تركيبات composition (مثل ChatNotificationsBridge)
/// لتقرّر "إذا الـ recipient يرى الرسالة الآن، لا تُنشئ سجلّ إشعار".
/// هذا منطق سياسة تطبيق — يعيش خارج Chat kit وخارج Notifications kit.</para>
///
/// <para>التطبيق يُسجّل تنفيذاً مبنيّاً فوق <c>IRealtimeChannelManager.IsOpen</c>
/// (راجع EjarChatPresenceProbe). تطبيقات أخرى قد تستخدم آليّة مختلفة
/// (Redis، last-seen timestamp، …) — العقد أحاديّ.</para>
/// </summary>
public interface IPresenceProbe
{
    Task<bool> IsUserActiveInConversationAsync(string userId, string conversationId, CancellationToken ct = default);
}
