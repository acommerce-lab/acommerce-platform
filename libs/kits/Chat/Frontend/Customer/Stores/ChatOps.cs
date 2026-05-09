using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Chat.Frontend.Customer.Stores;

/// <summary>
/// مَصنَع عَمَليّات الـ Chat kit عَلى جانب العَميل. كلّ مَنحى سُلوكيّ في
/// الـ store يُمَثَّل بِقَيد محاسبيّ مُنفَصِل: From (المُستَخدِم) → To (السيرفر)،
/// مَع tags تُحَدِّد سَياق العَمَليّة. compositions تَحقن مُعتَرضات عَلى
/// type tag لِتُضيف realtime broadcast أو optimistic update أو telemetry.
/// </summary>
public static class ChatOps
{
    /// <summary>قَيد جَلب قائمة المُحادَثات.</summary>
    public static Operation ListConversations() => Entry
        .Create("chat.conversations.list")
        .From("User:current", 1, ("role", "viewer"))
        .To("Server:chat",   1, ("role", "source"))
        .Build();

    /// <summary>قَيد فَتح مُحادَثة + جَلب رَسائلها (يُعَلِّم القراءة كَأَثَر جانبيّ).</summary>
    public static Operation OpenConversation(string conversationId) => Entry
        .Create("chat.conversation.open")
        .From("User:current", 1, ("role", "reader"))
        .To($"Conversation:{conversationId}", 1, ("role", "subject"))
        .Tag("id", conversationId)
        .Build();

    /// <summary>قَيد دُخول المُستَخدِم مُحادَثة (presence + mark-as-read).</summary>
    public static Operation EnterConversation(string conversationId) => Entry
        .Create("chat.enter")
        .From("User:current", 1, ("role", "presence"))
        .To($"Conversation:{conversationId}", 1, ("role", "subject"))
        .Tag("id", conversationId)
        .Build();

    /// <summary>قَيد إرسال رَسالة. composition Realtime تَحقن مُعتَرضاً
    /// عَلى هذا النَوع لِتَبُثّها لِبَقيّة الأَطراف عَبر الـ hub.</summary>
    public static Operation SendMessage(string conversationId, string body) => Entry
        .Create("chat.message.send")
        .From("User:current", 1, ("role", "sender"))
        .To($"Conversation:{conversationId}", 1, ("role", "destination"))
        .Tag("id",   conversationId)
        .Tag("body", body)
        .Tag("realtime_broadcast", "true")
        .Build();
}
