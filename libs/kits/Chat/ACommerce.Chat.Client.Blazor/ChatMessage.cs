using System.Text.Json.Serialization;
using ACommerce.Chat.Operations;

namespace ACommerce.Chat.Client.Blazor;

/// <summary>
/// تمثيل واحد محايد لرسالة دردشة يستوفي <see cref="IChatMessage"/>.
/// التطبيقات يمكنها استعمال هذا مباشرةً، أو تعريف نوعها الخاصّ الذي يُحقّق
/// نفس الواجهة (Law 6 المُعدَّل).
/// </summary>
public sealed record ChatMessage(
    [property: JsonPropertyName("id")]             string Id,
    [property: JsonPropertyName("conversationId")] string ConversationId,
    [property: JsonPropertyName("senderPartyId")]  string SenderPartyId,
    [property: JsonPropertyName("body")]           string Body,
    [property: JsonPropertyName("sentAt")]         DateTime SentAt,
    [property: JsonPropertyName("readAt")]         DateTime? ReadAt = null
) : IChatMessage;
