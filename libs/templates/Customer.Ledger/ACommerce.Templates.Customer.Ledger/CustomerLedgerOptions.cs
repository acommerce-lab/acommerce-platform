namespace ACommerce.Templates.Customer.Ledger;

/// <summary>
/// إعدادات قالَب Customer Ledger. التَطبيق يُمَرِّر:
/// <list type="bullet">
///   <item>إعدادات Auth (HttpClientName، StorageKey، Scheme) — إجباريّة.</item>
///   <item>قائمة المُضيفين المسموح بها لـ external URLs (CDN, storage…).</item>
///   <item>(اختياريّ) override لأيّ route مِن routes القالَب الافتراضيّة.</item>
///   <item>(اختياريّ) إخفاء routes لا يَحتاجها التَطبيق.</item>
/// </list>
/// </summary>
public sealed class CustomerLedgerOptions
{
    /// <summary>اسم HttpClientFactory client (مَثلاً "ejar").</summary>
    public string HttpClientName { get; set; } = "";

    /// <summary>مَفتاح localStorage لِحَفظ JWT (مَثلاً "ejar.v2.auth").</summary>
    public string StorageKey { get; set; } = "";

    /// <summary>اسم authentication scheme في ClaimsIdentity (مَثلاً "EjarV2Auth").</summary>
    public string Scheme { get; set; } = "";

    /// <summary>هَل يُسَجِّل القالَب AddClientAuth داخِليّاً؟ (افتراضيّ: نَعَم).
    /// التَطبيق يُمكنه إيقافه لو سَجَّل Auth بنَفسه.</summary>
    public bool RegisterAuth { get; set; } = true;

    /// <summary>المُضيفون المسموح بهم لـ external URLs (CDN، storage).</summary>
    public List<string> UrlAllowlist { get; } = new();

    /// <summary>routes إضافيّة خاصّة بالتَطبيق (about، terms، home مَخصَّص...).</summary>
    public List<(string Route, Type Component, bool RequiresAuth)> ExtraPages { get; } = new();

    /// <summary>أَسماء routes مِن القالَب الافتراضيّة لِيَتمّ تَخَطّيها.
    /// مَثلاً <c>"chat"</c> لو التَطبيق لا يَدعَم chat.</summary>
    public HashSet<string> ExcludedRoutes { get; } = new();

    /// <summary>override لـ binding store واحِد. مَثلاً تَطبيق Ejar V2 يَستَخدِم
    /// <c>RealtimeChatStore</c> بَدَل <c>DefaultChatStore</c> ⇒ يُسَجِّله ثُمّ يُمَرِّر <c>typeof(RealtimeChatStore)</c>.
    /// مَفتاح الـ Dictionary هو اسم الـ binding (<c>"chat"</c>، <c>"listings"</c>…).</summary>
    public Dictionary<string, Type> StoreOverrides { get; } = new();
}
