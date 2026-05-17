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

/// <summary>
/// عَرض المُحادَثَة في صَفحَة الـ inbox — صَريح بِالأَنواع. كانَ الـ
/// <c>ChatController.ListMine</c> سابِقاً يَستَخدِم <c>Cast&lt;object&gt;()</c>
/// لِإجبار JSON عَلى استِخدام runtime type (لِأَنّ <see cref="IChatConversation"/>
/// يَكشِف <c>Id + ParticipantPartyIds</c> فَقَط، فالواجِهَة الأَمامِيَّة كانَت
/// تَستَلِم rows فارِغَة). هذه الواجِهَة تَضَع كُلّ الحُقول المَطلوبَة في
/// عَقد مَوثَّق ⇒ السيريالايزر يُسلسِل عَبر typed surface بِلا hacks.
///
/// <para><b>2-party vs n-party</b>: الـ <c>OwnerId</c>/<c>PartnerId</c>
/// كافِيَة لِسيناريو الـ 2-party الَّذي تَستَهلِكه <c>Chats.razor</c> حالِيّاً.
/// التَطبيقات الَّتي تَحتاج n-party تَستَعمِل <see cref="IChatConversation.ParticipantPartyIds"/>
/// المُورَّثَة.</para>
/// </summary>
public interface IChatConversationView : IChatConversation
{
    string  OwnerId       { get; }
    string  OwnerName     { get; }
    string? OwnerAvatar   { get; }
    string  PartnerId     { get; }
    string  PartnerName   { get; }
    string? PartnerAvatar { get; }
    string  Subject       { get; }
    string? ListingId     { get; }
    DateTime LastAt       { get; }
    int     UnreadCount   { get; }
    string? LastMessage   { get; }
    DateTime? LastMessageAt { get; }
    /// <summary><c>true</c> لَو في رَسائل غَير مَقروءَة بِنَظَر المُشاهِد
    /// الحالي. الـ store يَختار العَدّاد الصَّحيح حَسب
    /// <c>viewerUserId = caller</c>.</summary>
    bool    HasMyUnread   { get; }
}

/// <summary>تَنفيذ POCO نَقي لِـ <see cref="IChatConversationView"/>. الـ
/// stores تَستَعمِله كَ wire shape مُباشَرَةً أَو تُنَفِّذ الواجِهَة عَلى
/// كَيان داخِلي خاصّ بِها.</summary>
public sealed record ChatConversationView(
    string Id,
    IReadOnlyList<string> ParticipantPartyIds,
    string OwnerId,
    string OwnerName,
    string? OwnerAvatar,
    string PartnerId,
    string PartnerName,
    string? PartnerAvatar,
    string Subject,
    string? ListingId,
    DateTime LastAt,
    int UnreadCount,
    string? LastMessage,
    DateTime? LastMessageAt,
    bool HasMyUnread) : IChatConversationView;
