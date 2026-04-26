namespace ACommerce.Realtime.Operations.Abstractions;

/// <summary>
/// يدير اشتراكات القنوات لكلّ مستخدم: <c>(userId, channelId)</c> هو الوحدة.
///
/// <para>القناة هنا مفهوم منطقيّ على مستوى التطبيق، تُنشأ عند انضمام أوّل
/// مستخدم وتنتهي بإغلاق صريح أو انقضاء مهلة خمول. تُبنى فوق
/// <see cref="IRealtimeTransport"/> لذا تعمل مع أيّ مزوّد (SignalR / InMemory / ...).</para>
///
/// <para>التطبيقات تستعمل <see cref="OnChannelOpened"/> و <see cref="OnChannelClosed"/>
/// لربط منطقها الخاصّ — مثل: عند فتح <c>chat:conv:X</c> أغلق <c>notif:conv:X</c>،
/// والعكس صحيح. مكتبات الإشعارات والدردشة لا تعرف بعضها بعضاً؛ التطبيق هو
/// الذي يربطهما عبر هذه الأحداث.</para>
/// </summary>
public interface IRealtimeChannelManager
{
    /// <summary>يفتح اشتراكاً للمستخدم على القناة. يُطلِق <c>OnChannelOpened</c>.</summary>
    Task OpenAsync(
        string userId,
        string connectionId,
        string channelId,
        RealtimeChannelOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// يغلق اشتراك المستخدم على القناة. يُطلِق <c>OnChannelClosed</c>
    /// بسبب <see cref="RealtimeChannelCloseReason.Explicit"/>.
    /// idempotent — استدعاء مكرّر لقناة مغلقة لا يفعل شيئاً.
    /// </summary>
    Task CloseAsync(string userId, string channelId, CancellationToken ct = default);

    /// <summary>
    /// يُجدِّد مؤقّت الخمول للقناة (إن كانت ذات مهلة). يُستدعى عند أيّ نشاط
    /// (إرسال/استقبال رسالة، نبضة من العميل…).
    /// </summary>
    Task RecordActivityAsync(string userId, string channelId, CancellationToken ct = default);

    /// <summary>هل القناة مفتوحة حاليّاً للمستخدم؟</summary>
    bool IsOpen(string userId, string channelId);

    /// <summary>
    /// يربط منطقاً تطبيقيّاً يعمل عند فتح أيّ قناة معرّفها يطابق <paramref name="channelIdPattern"/>.
    /// النمط يقبل النجمة <c>*</c> كـ wildcard للقطع. أمثلة:
    /// <list type="bullet">
    ///   <item><c>"chat:conv:*"</c> يلتقط كل قنوات الدردشة</item>
    ///   <item><c>"chat:conv:abc-123"</c> يلتقط محادثة بعينها</item>
    /// </list>
    /// </summary>
    void OnChannelOpened(string channelIdPattern, Func<RealtimeChannelEvent, Task> handler);

    /// <summary>يربط منطقاً عند إغلاق قناة (لأيّ سبب — explicit/idle/disconnect).</summary>
    void OnChannelClosed(string channelIdPattern, Func<RealtimeChannelEvent, Task> handler);

    /// <summary>
    /// يغلق كلّ اشتراكات المستخدم — تُستدعى من معالج Disconnect في المزوّد.
    /// تُطلَق <c>OnChannelClosed</c> بسبب <see cref="RealtimeChannelCloseReason.Disconnect"/>
    /// لكلّ قناة مفتوحة.
    /// </summary>
    Task CloseAllForConnectionAsync(string userId, string connectionId, CancellationToken ct = default);
}
