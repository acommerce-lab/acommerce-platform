namespace ACommerce.Realtime.Operations.Abstractions;

/// <summary>
/// حدث دورة حياة اشتراك قناة لمستخدم.
/// </summary>
/// <param name="UserId">المستخدم صاحب الاشتراك.</param>
/// <param name="ConnectionId">معرّف الاتصال الذي بدأ به الاشتراك (اختياريّ — قد يكون فارغاً عند الإغلاق بسبب الخمول).</param>
/// <param name="ChannelId">معرّف القناة (مثلاً <c>chat:conv:abc</c> أو <c>notif:conv:abc</c>).</param>
/// <param name="Reason">سبب الحدث.</param>
public sealed record RealtimeChannelEvent(
    string UserId,
    string? ConnectionId,
    string ChannelId,
    RealtimeChannelCloseReason? Reason = null);

/// <summary>سبب إغلاق قناة (يصاحب <c>OnChannelClosed</c> فقط).</summary>
public enum RealtimeChannelCloseReason
{
    /// <summary>طلب صريح من التطبيق (<see cref="IRealtimeChannelManager.CloseAsync"/>).</summary>
    Explicit,
    /// <summary>انقضاء مهلة الخمول.</summary>
    Idle,
    /// <summary>قطع الاتصال (Disconnect).</summary>
    Disconnect
}
