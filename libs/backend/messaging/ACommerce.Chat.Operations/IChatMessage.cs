namespace ACommerce.Chat.Operations;

/// <summary>
/// عقد الرسالة. كيان النطاق في التطبيق <b>يرث هذه الواجهة</b> ويضيف ما يشاء
/// — لا نفرض DTO ولا نُلزِم التطبيقات بشكل تخزين معيَّن (Law 6 المُعدَّل).
/// المهمّ أنّ الخصائص الستّ أدناه متوفّرة لتعمل قنوات الدردشة وفهرسة
/// المحادثات بشكل موحَّد.
/// </summary>
public interface IChatMessage
{
    /// <summary>المعرّف الفريد للرسالة (Guid أو string).</summary>
    string Id { get; }

    /// <summary>معرّف المحادثة التي تنتمي إليها الرسالة. مستخدَم في تسمية قناة الدردشة.</summary>
    string ConversationId { get; }

    /// <summary>معرّف الطرف المرسِل.</summary>
    string SenderPartyId { get; }

    /// <summary>نصّ الرسالة (يكفي للحالة الحاليّة — مرفقات تأتي لاحقاً).</summary>
    string Body { get; }

    /// <summary>وقت الإرسال UTC.</summary>
    DateTime SentAt { get; }

    /// <summary>وقت قراءة المستلِم (إن قُرأت). null = غير مقروءة بعد.</summary>
    DateTime? ReadAt { get; }
}

/// <summary>
/// عقد المحادثة. كيان النطاق يرث هذه الواجهة. خاصيّة <see cref="ParticipantPartyIds"/>
/// تستعمل لتحديد من يبثّ إليهم <see cref="IChatService.BroadcastNewMessageAsync"/>.
/// </summary>
public interface IChatConversation
{
    string Id { get; }
    IReadOnlyList<string> ParticipantPartyIds { get; }
}
