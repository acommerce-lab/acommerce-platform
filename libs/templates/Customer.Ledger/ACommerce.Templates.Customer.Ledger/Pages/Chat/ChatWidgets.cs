using ACommerce.Kits.Chat.Frontend.Customer.Widgets;

namespace ACommerce.Kits.Chat.Frontend.Customer;

/// <summary>
/// نقطة الدخول الوحيدة لاستهلاك widgets الـ Chat. التطبيق يَستعملها مع
/// <c>AddAppPages(p =&gt; p.Add("/chat", ChatWidgets.Inbox))</c> أو يَلفّها
/// داخل layout/composition خاصّة.
///
/// <para>الفلسفة: الكيت لا يَفرض routes — يَكشف widgets فقط (مكوّنات Razor
/// بدون <c>@page</c>). التطبيق هو الذي يَختار: صفحة كاملة، side drawer،
/// modal، أو composition مع widgets من kits أخرى. هذا يَمنح أقصى مرونة
/// إعادة التركيب بأقلّ افتراضات على الـ UX.</para>
/// </summary>
public static class ChatWidgets
{
    /// <summary>قائمة المحادثات (inbox). يَستهلك <c>IChatStore</c>.</summary>
    public static Type Inbox       => typeof(AcChatInboxWidget);

    /// <summary>غرفة محادثة واحدة (رسائل + composer). يَأخذ <c>Id</c> route param.</summary>
    public static Type Room        => typeof(AcChatRoomWidget);

    /// <summary>composer مستقلّ (input + send). قابل للاستعمال خارج Room.</summary>
    public static Type Composer    => typeof(AcChatComposerWidget);

    /// <summary>شارة عَدّاد رسائل غير مقروءة — للـ navbar/dashboard.</summary>
    public static Type UnreadBadge => typeof(AcChatUnreadBadgeWidget);
}
