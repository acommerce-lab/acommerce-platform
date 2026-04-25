using ACommerce.Chat.Operations;

namespace ACommerce.Chat.Client.Blazor;

/// <summary>
/// عميل دردشة المُتصفّح. يُحقَن في صفحات Razor (<c>@inject IChatClient Chat</c>).
///
/// <para><b>دورة الحياة المتوقَّعة من الصفحة:</b></para>
/// <list type="number">
///   <item>عند تركيب الصفحة (<c>OnAfterRenderAsync(firstRender: true)</c>) → <see cref="EnterAsync"/></item>
///   <item>عند الضغط على زرّ الإرسال → <see cref="SendAsync"/></item>
///   <item>عند Dispose الصفحة (الرجوع/التنقّل) → <see cref="LeaveAsync"/></item>
///   <item>اشترك بـ <see cref="MessageReceived"/> لتحديث قائمة الرسائل عند الاستلام لحظيّاً</item>
/// </list>
///
/// <para>التطبيق يربط منطق إغلاق الدردشة عند خمول/إغلاق التطبيق عبر:</para>
/// <list type="bullet">
///   <item>قاعدة <c>BeforeUnload</c> JS event يستدعي <c>chatClient.LeaveAsync()</c></item>
///   <item>الخدمة الخلفيّة تكتشف الانقطاع تلقائيّاً عبر Hub.OnDisconnectedAsync</item>
///   <item>Idle timeout على القناة في الخلفيّة يُغلِقها بدون تدخّل العميل</item>
/// </list>
/// </summary>
public interface IChatClient
{
    /// <summary>المحادثة المفتوحة حاليّاً (إن وُجدت).</summary>
    string? ActiveConversationId { get; }

    /// <summary>يصدر عند وصول رسالة لحظيّة <b>للمحادثة المفتوحة فقط</b>.
    /// رسائل المحادثات الأخرى تمرّ عبر قناة الإشعارات لا هنا.</summary>
    event Action<IChatMessage>? MessageReceived;

    /// <summary>يفتح المحادثة: يستدعي الـ Backend ليُعدّ القناة للمستخدم،
    /// ويعيّن <see cref="ActiveConversationId"/>.</summary>
    Task EnterAsync(string conversationId, CancellationToken ct = default);

    /// <summary>يغلق المحادثة الحاليّة (إن كانت مفتوحة). idempotent.</summary>
    Task LeaveAsync(CancellationToken ct = default);

    /// <summary>يرسل رسالة في المحادثة الحاليّة عبر الـ Backend.
    /// <see cref="MessageReceived"/> سيُطلَق عند استلام البثّ المرتدّ.</summary>
    Task SendAsync(string body, CancellationToken ct = default);

    /// <summary>
    /// Hook يستدعيه adapter الـ realtime في التطبيق متى وصلت رسالة من البثّ.
    /// عميل الدردشة يقرّر هل يطلق <see cref="MessageReceived"/> (إذا كانت
    /// المحادثة مفتوحة) أو يتجاهلها (إذا كانت لمحادثة أخرى — في هذه الحالة
    /// قناة الإشعارات هي التي تتولّاها).
    /// </summary>
    void OnRealtimeMessage(IChatMessage message);
}
