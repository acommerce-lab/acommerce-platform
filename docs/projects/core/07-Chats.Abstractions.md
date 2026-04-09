# ACommerce.Chats.Abstractions

## نظرة عامة
مكتبة تجريدات نظام المحادثات الشاملة. تدعم المحادثات الفردية والجماعية مع ميزات مثل الرد على الرسائل، المرفقات، مؤشر الكتابة، وحالة التواجد (Presence).

## الموقع
`/Core/ACommerce.Chats.Abstractions`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`
- `MediatR`

---

## الواجهات (Providers)

### IChatProvider
إدارة المحادثات:

```csharp
public interface IChatProvider
{
    // إنشاء محادثة جديدة
    Task<ChatDto> CreateChatAsync(CreateChatDto dto, CancellationToken cancellationToken = default);

    // الحصول على محادثة
    Task<ChatDto?> GetChatAsync(Guid chatId, CancellationToken cancellationToken = default);

    // الحصول على محادثات المستخدم
    Task<PagedResult<ChatDto>> GetUserChatsAsync(string userId, PaginationRequest request, CancellationToken cancellationToken = default);

    // تحديث محادثة
    Task<ChatDto> UpdateChatAsync(Guid chatId, UpdateChatDto dto, CancellationToken cancellationToken = default);

    // حذف محادثة
    Task DeleteChatAsync(Guid chatId, CancellationToken cancellationToken = default);

    // إضافة مشارك
    Task<ParticipantDto> AddParticipantAsync(Guid chatId, AddParticipantDto dto, CancellationToken cancellationToken = default);

    // إزالة مشارك
    Task RemoveParticipantAsync(Guid chatId, string userId, CancellationToken cancellationToken = default);

    // الحصول على المشاركين
    Task<List<ParticipantDto>> GetParticipantsAsync(Guid chatId, CancellationToken cancellationToken = default);
}
```

### IMessageProvider
إدارة الرسائل:

```csharp
public interface IMessageProvider
{
    // إرسال رسالة
    Task<MessageDto> SendMessageAsync(Guid chatId, SendMessageDto dto, CancellationToken cancellationToken = default);

    // الحصول على رسائل المحادثة
    Task<PagedResult<MessageDto>> GetMessagesAsync(Guid chatId, PaginationRequest request, CancellationToken cancellationToken = default);

    // الحصول على رسالة
    Task<MessageDto?> GetMessageAsync(Guid messageId, CancellationToken cancellationToken = default);

    // تحديث رسالة
    Task<MessageDto> UpdateMessageAsync(Guid messageId, UpdateMessageDto dto, CancellationToken cancellationToken = default);

    // حذف رسالة
    Task DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken = default);

    // تعليم الرسائل كمقروءة
    Task MarkAsReadAsync(Guid chatId, string userId, Guid? lastMessageId = null, CancellationToken cancellationToken = default);

    // البحث في الرسائل
    Task<PagedResult<MessageDto>> SearchMessagesAsync(Guid chatId, string searchQuery, PaginationRequest request, CancellationToken cancellationToken = default);
}
```

### IRealtimeChatProvider
التحديثات اللحظية:

```csharp
public interface IRealtimeChatProvider
{
    // إرسال رسالة لحظياً
    Task SendMessageToChat(Guid chatId, MessageDto message, CancellationToken cancellationToken = default);

    // إرسال مؤشر الكتابة
    Task SendTypingIndicator(Guid chatId, TypingIndicatorDto indicator, CancellationToken cancellationToken = default);

    // إشعار انضمام مشارك
    Task SendParticipantJoined(Guid chatId, ParticipantDto participant, CancellationToken cancellationToken = default);

    // إشعار مغادرة مشارك
    Task SendParticipantLeft(Guid chatId, string userId, CancellationToken cancellationToken = default);

    // تحديث حالة التواجد
    Task SendUserPresenceUpdate(string userId, bool isOnline, CancellationToken cancellationToken = default);

    // إشعار قراءة رسالة
    Task SendMessageRead(Guid chatId, string userId, Guid messageId, CancellationToken cancellationToken = default);
}
```

### IPresenceProvider
حالة التواجد (Online/Offline):

```csharp
public interface IPresenceProvider
{
    // تحديث حالة المستخدم
    Task UpdateUserPresenceAsync(string userId, bool isOnline, CancellationToken cancellationToken = default);

    // الحصول على حالة مستخدم
    Task<bool> GetUserPresenceAsync(string userId, CancellationToken cancellationToken = default);

    // الحصول على حالة عدة مستخدمين
    Task<Dictionary<string, bool>> GetUsersPresenceAsync(List<string> userIds, CancellationToken cancellationToken = default);
}
```

---

## DTOs

### ChatDto
```csharp
public class ChatDto
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public ChatType Type { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int ParticipantsCount { get; set; }
    public int UnreadMessagesCount { get; set; }
    public MessageDto? LastMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

### MessageDto
```csharp
public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string SenderId { get; set; }
    public string SenderName { get; set; }
    public string? SenderAvatar { get; set; }
    public string Content { get; set; }
    public MessageType Type { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public MessageDto? ReplyToMessage { get; set; }
    public List<string> Attachments { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public int ReadByCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### ParticipantDto
```csharp
public class ParticipantDto
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string? UserAvatar { get; set; }
    public ParticipantRole Role { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public Guid? LastSeenMessageId { get; set; }
    public int UnreadMessagesCount { get; set; }
    public bool IsMuted { get; set; }
    public bool IsPinned { get; set; }
    public DateTime JoinedAt { get; set; }
}
```

### CreateChatDto
```csharp
public class CreateChatDto
{
    public required string Title { get; set; }
    public ChatType Type { get; set; } = ChatType.Group;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public required string CreatorUserId { get; set; }
    public List<string> ParticipantUserIds { get; set; } = new();
}
```

### SendMessageDto
```csharp
public class SendMessageDto
{
    public required string SenderId { get; set; }
    public required string Content { get; set; }
    public MessageType Type { get; set; } = MessageType.Text;
    public Guid? ReplyToMessageId { get; set; }
    public List<string>? Attachments { get; set; }
}
```

### TypingIndicatorDto
```csharp
public class TypingIndicatorDto
{
    public Guid ChatId { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public bool IsTyping { get; set; }
}
```

### PaginationRequest
```csharp
public class PaginationRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
```

---

## التعدادات (Enums)

### ChatType

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Direct` | 1 | محادثة خاصة بين شخصين |
| `Group` | 2 | مجموعة |
| `Channel` | 3 | قناة (بث فقط) |
| `Support` | 4 | دعم فني |

### MessageType

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Text` | 1 | نص |
| `Image` | 2 | صورة |
| `File` | 3 | ملف |
| `Voice` | 4 | صوت |
| `Video` | 5 | فيديو |
| `Location` | 6 | موقع جغرافي |
| `System` | 7 | رسالة نظام |

### ParticipantRole

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Owner` | 1 | مالك (المنشئ) |
| `Admin` | 2 | مشرف |
| `Member` | 3 | عضو |
| `Guest` | 4 | ضيف |

---

## الأحداث (Events)

### MessageSentEvent
```csharp
public class MessageSentEvent : INotification
{
    public Guid ChatId { get; set; }
    public Guid MessageId { get; set; }
    public string SenderId { get; set; }
    public DateTime SentAt { get; set; }
}
```

### أحداث أخرى
- `ChatCreatedEvent` - إنشاء محادثة
- `MessageReadEvent` - قراءة رسالة
- `MessageDeletedEvent` - حذف رسالة
- `ParticipantJoinedEvent` - انضمام مشارك
- `ParticipantLeftEvent` - مغادرة مشارك

---

## بنية الملفات
```
ACommerce.Chats.Abstractions/
├── Providers/
│   ├── IChatProvider.cs
│   ├── IMessageProvider.cs
│   ├── IRealtimeChatProvider.cs
│   └── IPresenceProvider.cs
├── DTOs/
│   ├── ChatDto.cs
│   ├── MessageDto.cs
│   ├── ParticipantDto.cs
│   ├── CreateChatDto.cs
│   ├── SendMessageDto.cs
│   ├── TypingIndicatorDto.cs
│   ├── UpdateMessageDto.cs
│   ├── MarkAsReadRequest.cs
│   └── PaginationRequest.cs
├── Enums/
│   ├── ChatType.cs
│   ├── MessageType.cs
│   └── ParticipantRole.cs
└── Events/
    ├── MessageSentEvent.cs
    ├── MessageReadEvent.cs
    ├── MessageDeletedEvent.cs
    ├── ChatCreatedEvent.cs
    ├── ParticipantJoinedEvent.cs
    └── ParticipantLeftEvent.cs
```

---

## مثال استخدام

### إنشاء محادثة جماعية
```csharp
var chat = await chatProvider.CreateChatAsync(new CreateChatDto
{
    Title = "فريق المبيعات",
    Type = ChatType.Group,
    Description = "مجموعة فريق المبيعات",
    CreatorUserId = currentUserId,
    ParticipantUserIds = new List<string> { "user1", "user2", "user3" }
});
```

### إرسال رسالة مع رد
```csharp
var message = await messageProvider.SendMessageAsync(chatId, new SendMessageDto
{
    SenderId = currentUserId,
    Content = "شكراً على الرد السريع!",
    Type = MessageType.Text,
    ReplyToMessageId = originalMessageId
});
```

### إرسال مؤشر الكتابة
```csharp
await realtimeChatProvider.SendTypingIndicator(chatId, new TypingIndicatorDto
{
    ChatId = chatId,
    UserId = currentUserId,
    UserName = "أحمد",
    IsTyping = true
});
```

### تعليم الرسائل كمقروءة
```csharp
await messageProvider.MarkAsReadAsync(chatId, currentUserId, lastMessageId);
```

---

## ملاحظات تقنية

1. **MediatR Events**: الأحداث تنفذ `INotification` للتكامل مع MediatR
2. **Reply Support**: دعم الرد على رسائل سابقة
3. **Attachments**: دعم المرفقات المتعددة
4. **Presence**: نظام متابعة حالة التواجد
5. **Typing Indicator**: مؤشر الكتابة في الوقت الفعلي
6. **Read Receipts**: متابعة قراءة الرسائل
7. **Pagination**: دعم التصفح لجميع القوائم
