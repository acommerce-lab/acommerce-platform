namespace ACommerce.Kits.Chat.Backend;

/// <summary>
/// خيارات تخصيص الـ Chat Kit. كلّ تطبيق يضبط ما يلزمه عبر
/// <c>AddChatKit&lt;TStore&gt;(opts =&gt; ...)</c>.
/// </summary>
public sealed class ChatKitOptions
{
    /// <summary>
    /// بادئة معرّف الطرف في الـ realtime (تُستعمل عند فتح/إغلاق القناة).
    /// أمثلة: "User"، "Provider"، "Admin"، "Vendor". الافتراض: "User".
    /// </summary>
    public string PartyKind { get; set; } = "User";

    /// <summary>الحدّ الأعلى لطول نصّ الرسالة. الافتراض: 4000 محرف.</summary>
    public int MaxMessageLength { get; set; } = 4000;

    /// <summary>
    /// مهلة خمول قناة الدردشة. بعد انقضائها يُغلِق الـ realtime channel manager
    /// قناة المستخدم تلقائيّاً ويعيد فتح قناة إشعارات المحادثة. الافتراض: دقيقتان.
    /// </summary>
    public TimeSpan ChatIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
