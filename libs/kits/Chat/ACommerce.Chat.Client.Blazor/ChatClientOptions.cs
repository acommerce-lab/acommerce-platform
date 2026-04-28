namespace ACommerce.Chat.Client.Blazor;

public sealed class ChatClientOptions
{
    /// <summary>
    /// المسار النسبيّ لنقطة دخول/مغادرة قناة الدردشة في الـ Backend.
    /// النموذج: <c>{convId}</c> placeholder. الافتراض: <c>"/chat/{convId}/enter"</c>.
    /// </summary>
    public string EnterPathTemplate { get; set; } = "/chat/{convId}/enter";

    /// <summary>الافتراض: <c>"/chat/{convId}/leave"</c>.</summary>
    public string LeavePathTemplate { get; set; } = "/chat/{convId}/leave";

    /// <summary>
    /// مسار إرسال الرسائل (يطابق الـ endpoint القائم في كلّ تطبيق).
    /// الافتراض: <c>"/conversations/{convId}/messages"</c>.
    /// </summary>
    public string SendPathTemplate { get; set; } = "/conversations/{convId}/messages";

    /// <summary>اسم HttpClient المسجّل (إذا كان التطبيق يستعمل named client).</summary>
    public string? HttpClientName { get; set; }
}
