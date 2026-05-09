namespace ACommerce.Chat.Operations;

/// <summary>
/// تطبيق POCO نقيّ لـ <see cref="IChatMessage"/> — لا EF، لا DB، لا state.
/// يُستخدم في <c>ChatController.Send</c> كـ "حدث المحادثة الأصيل" في
/// قيد <c>message.send</c>: الـ Execute body يبنيه ويضعه على
/// <c>ctx.WithEntity&lt;IChatMessage&gt;()</c> فيتدفّق لكلّ interceptor
/// (broadcast، notification.create، FCM) دون أيّ افتراض بشأن وجود جدول
/// <c>Messages</c> في DB.
///
/// <para>الفكرة المعماريّة: الرسالة كحدث OAM مستقلّة عن تخزينها.
/// التطبيق يستطيع إسقاط <c>IChatStore</c> أو تركيب
/// <see cref="ACommerce.OperationEngine.DataInterceptors.CrudActionInterceptor"/>
/// بدلاً منه — الـ envelope ينجح، الإشعار يُرسل، البثّ ينطلق، حتى لو لم
/// تُخزَّن الرسالة في DB إطلاقاً.</para>
/// </summary>
public sealed record InMemoryChatMessage(
    string Id,
    string ConversationId,
    string SenderPartyId,
    string Body,
    DateTime SentAt,
    DateTime? ReadAt = null
) : IChatMessage;
