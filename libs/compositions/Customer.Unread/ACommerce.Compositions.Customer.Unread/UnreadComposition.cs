using ACommerce.Kits.Chat.Frontend.Customer.Stores;
using ACommerce.Kits.Notifications.Frontend.Customer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Customer.Unread;

/// <summary>
/// composition يَجمَع عَدّادات غير المَقروء مِن أكثَر مِن kit في طَبَقة
/// واحِدة. يَعرِض إجماليّ غير-المَقروء + بَيان كلّ مَصدَر، ويُطلِق
/// <see cref="Changed"/> عِند أيّ تَحديث في أيّ مِن المَصادِر.
///
/// <para>الكيتس لا تَعرِف عَن بَعضها — هذا هو دور composition: يَلفّ
/// <see cref="IChatStore"/> + <see cref="INotificationsStore"/> ويَكشِف
/// shape أَعلى يُغَذّي شارات navbar / dashboard.</para>
///
/// <para>التَطبيق:
/// <code>
/// services.AddCustomerUnreadComposition();
/// // ثُمّ في صَفحَة:
/// @inject UnreadComposition Unread
/// &lt;span class="badge"&gt;@Unread.Total&lt;/span&gt;
/// </code></para>
/// </summary>
public sealed class UnreadComposition : IDisposable
{
    private readonly IChatStore _chat;
    private readonly INotificationsStore _notif;

    public UnreadComposition(IChatStore chat, INotificationsStore notif)
    {
        _chat = chat;
        _notif = notif;
        _chat.Changed += OnChanged;
        _notif.Changed += OnChanged;
    }

    /// <summary>عَدد الرَسائل غير المَقروءة عَبر كلّ المُحادَثات.</summary>
    public int ChatUnread => _chat.UnreadTotal;

    /// <summary>عَدد الإشعارات غير المَقروءة.</summary>
    public int NotifUnread => _notif.UnreadCount;

    /// <summary>الإجماليّ — لِشارَة واحدة جامِعَة.</summary>
    public int Total => ChatUnread + NotifUnread;

    /// <summary>
    /// id المُحادَثة المَفتوحَة حاليّاً (أَو null). يُستَخدَم في الـ realtime
    /// ingestor: رَسائل واصِلَة لِنَفس الـ id لا تُزيد العَدّاد لأنّ
    /// المُستَخدِم يَراها مُباشَرَةً. صَفحَة <c>ChatRoom</c> تَكتُبها عِند الدُخول
    /// وتَمسَحها عِند المُغادَرَة.
    /// </summary>
    public string? ActiveConversationId { get; set; }

    /// <summary>
    /// يَرتِقي المَنبَع: يَستَدعي <c>IChatStore.LoadConversationsAsync</c> +
    /// <c>INotificationsStore.LoadAsync</c> بِالتَوازي. شارات navbar تَستَدعيه
    /// عِند تَسجيل الدُخول أَو re-mount لِلـ shell بَعد reconnect.
    /// </summary>
    public Task RefreshAsync(CancellationToken ct = default) =>
        Task.WhenAll(
            _chat.LoadConversationsAsync(ct),
            _notif.LoadAsync(ct));

    public event Action? Changed;

    private void OnChanged() => Changed?.Invoke();

    public void Dispose()
    {
        _chat.Changed -= OnChanged;
        _notif.Changed -= OnChanged;
    }
}

public static class UnreadCompositionExtensions
{
    public static IServiceCollection AddCustomerUnreadComposition(this IServiceCollection services)
    {
        services.AddScoped<UnreadComposition>();
        return services;
    }
}
