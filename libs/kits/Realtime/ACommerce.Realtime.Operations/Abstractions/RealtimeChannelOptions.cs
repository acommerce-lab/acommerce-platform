namespace ACommerce.Realtime.Operations.Abstractions;

/// <summary>
/// خيارات إنشاء اشتراك قناة لمستخدم.
/// </summary>
public sealed class RealtimeChannelOptions
{
    /// <summary>
    /// مهلة الخمول. إن مرّت دون استدعاء <see cref="IRealtimeChannelManager.RecordActivityAsync"/>
    /// تُغلَق القناة تلقائيّاً ويُطلَق <c>OnChannelClosed</c> بسبب
    /// <see cref="RealtimeChannelCloseReason.Idle"/>. <c>null</c> = قناة مفتوحة دائماً
    /// (مثاليّة لقنوات الإشعارات).
    /// </summary>
    public TimeSpan? IdleTimeout { get; init; }
}
