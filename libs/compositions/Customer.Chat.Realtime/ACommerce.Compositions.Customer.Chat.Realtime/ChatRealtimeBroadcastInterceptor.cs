using ACommerce.Chat.Operations;
using ACommerce.Kits.Chat.Frontend.Customer.Stores;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;

namespace ACommerce.Compositions.Customer.Chat.Realtime;

/// <summary>
/// OAM interceptor يَعمَل في طَور <see cref="InterceptorPhase.Post"/> عَلى قَيد
/// <c>http.send</c> الذي يَحمل <c>chat.message.send</c> داخِله. عِند النَجاح،
/// يَستَدعي <see cref="IChatRealtimeBroadcaster"/> لِبَثّ الرَسالَة لِبَقيّة
/// الأَطراف عَبر hub التَطبيق (SignalR / WebSocket / إلخ).
///
/// <para>هذه هي قُوّة OAM في الكيتس: الـ kit نَفسه لا يَعلَم عَن realtime؛
/// composition تَلتَقِط القَيد بَعد إرساله وتُضيف بَثّ الزَمَن الحَقيقيّ
/// كَأَثَر جانِبيّ. لو دَعّت إلى تَعطيل الـ realtime — ما عَلَيك إلّا
/// عَدَم تَسجيل الـ composition. الكيت يَعمَل كَما هو.</para>
/// </summary>
public sealed class ChatRealtimeBroadcastInterceptor : IOperationInterceptor
{
    private readonly IChatRealtimeBroadcaster _broadcaster;
    public ChatRealtimeBroadcastInterceptor(IChatRealtimeBroadcaster broadcaster)
        => _broadcaster = broadcaster;

    public string Name => "chat-realtime-broadcast";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public bool AppliesTo(Operation op)
    {
        if (op.Type != "http.send") return false;
        return op.GetTagValue("embedded_op_type") == "chat.message.send";
    }

    public async Task<AnalyzerResult> InterceptAsync(
        OperationContext context,
        OperationResult? result = null)
    {
        var status = context.Get<int?>("status_code");
        if (status is < 200 or >= 300) return AnalyzerResult.Pass();

        var convId = context.Operation.GetTagValue("id");
        var body   = context.Operation.GetTagValue("body");
        if (convId is null || body is null) return AnalyzerResult.Pass();

        try { await _broadcaster.BroadcastSendAsync(convId, body, context.CancellationToken); }
        catch { /* غير قاتِل — الرَسالَة وَصَلَت السيرفر، البَثّ للأَجهزَة الأُخرى مُتَأَخِّر */ }

        return AnalyzerResult.Pass();
    }
}

/// <summary>
/// عَقد لِلتَطبيق لِبَثّ أحداث chat عَبر hub خاصّ بِه. التَطبيق يُسَجِّل
/// تَنفيذاً يَلفّ SignalR connection / WebSocket / Firebase Realtime / إلخ.
/// </summary>
public interface IChatRealtimeBroadcaster
{
    Task BroadcastSendAsync(string conversationId, string body, CancellationToken ct = default);
}

/// <summary>
/// مَدخَل realtime واردة: hub التَطبيق يَستَدعيها عِند وُصول رَسالة مِن
/// طَرَف آخَر. تَدفَع الرَسالَة إلى <see cref="DefaultChatStore"/> فتُحَدِّث
/// الحالة المَحَلّيّة بدون أن تَدور عَلى السيرفر مَجدَّداً.
/// </summary>
public sealed class ChatRealtimeIngestor
{
    private readonly DefaultChatStore _store;
    public ChatRealtimeIngestor(IChatStore store) => _store = (DefaultChatStore)store;

    public void OnMessageReceived(IChatMessage message) =>
        _store.IngestRealtimeMessage(message);
}
