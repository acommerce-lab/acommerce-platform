# COMPOSITION MODEL — معماريّة التراكيب المحاسبيّة

> الوضع المستهدَف: كلّ kit يحوي **محاسبة بحتة** لمجاله، بلا أيّ علم بمكتبات
> أخرى. التركيب (composition) — أي ضمّ Chat + Notifications + Realtime
> مثلاً — يحدث **خارج** الـ kits المعنيّة عبر ضخّ `IOperationInterceptor`
> يُلتقَط عبر `op.Type` و الأوسمة. التركيب نفسه يصبح مكوِّناً قابلاً للتركيب
> فوقه. كلّ تعريف يأتي عبر **أنواع معرَّفة** (records/structs/enums) لا
> عبر سلاسل سحريّة.

ملاحظة: هذه الوثيقة **خطّة**. لم تُنفَّذ بعد (سيتمّ على مراحل). راجع
"المراحل" في الأسفل لخارطة الطريق.

---

## ١) المشكلة الراهنة — ما يُؤلِم

### ١-أ. سلاسل سحريّة في كلّ مكان

```csharp
Entry.Create("message.send")
    .Tag("kind", "support")
    .Tag("ticket_id", id)
    .Tag(SupportTags.Kind, SupportTags.KindSupport);  // ← خَلطَة: ثوابت + سحر
```

الـ Type والـ Tag مفاتيحاً وقيماً نصوص. خطأ كتابيّ بحرف واحد →
interceptor يفوّت العمليّة بصمت.

### ١-ب. Side effects مدمَجة في الـ stores

`EjarCustomerChatStore.AppendMessageAsync` يكتب الرسالة + يبثّ realtime +
ينشئ DB notification + يُرسل FCM push — أربعة أغراض في دالّة واحدة.
إعادة الاستخدام صعبة، الفصل غير ممكن.

### ١-ج. تركيب الـ kits لا يحدث "من خارجها"

Support kit يحقن `IChatStore` كاعتماد (يعرف بـ Chat). Chat kit يحقن
`IRealtimeTransport` (يعرف بـ Realtime). Notifications kit يخزِّن في DB.
الترتيب صحيح يعمل، لكنّه ليس **محاسبيّاً نقيّاً**: لو أردت دردشة بلا
realtime لا يتحقّق إلاّ بـ provider null. لو أردت إشعارات بدون chat لن
ترتبطا أصلاً.

### ١-د. لا "مثيل تركيب" قابل لإعادة الاستخدام

نسجِّل interceptors واحداً واحداً في `Program.cs`. لا توجد وحدة "تركيب"
يُمكن استدعاؤها مرّتَين أو أكثر بإعدادات مختلفة (مثل: تركيب Chat الذي
يدعم realtime لجمهور التطبيق + تركيب Chat بدون realtime لـ admin).

---

## ٢) المبدأ — ثلاث طبقات

```
┌─────────────────────────── App (Ejar.Api) ─────────────────────────┐
│  Program.cs:                                                       │
│    services.AddComposition<EjarCustomerComposition>();             │
│    (سطر واحد ينطلق منه كلّ شيء)                                  │
└────────────────────────────────────────────────────────────────────┘
                              ↑
┌────────────────────── Compositions (libs/compositions) ────────────┐
│  EjarCustomerComposition = ChatRealtimeNotifications + Support     │
│  ChatRealtimeNotifications = Chat + Notifications + Realtime       │
│  Auth.WithSmsOtp = Auth + TwoFactor + Sms                          │
│  هذا يُعرَّف بـ IInterceptorBundle + ICompositionDescriptor.        │
│  الكود هنا interceptors فقط — لا entities ولا controllers.         │
└────────────────────────────────────────────────────────────────────┘
                              ↑
┌──────────────────────── Kits (libs/kits) — pure ───────────────────┐
│  Chat: IChatStore (persist only). لا broadcast، لا notification.   │
│  Notifications: INotificationChannel + INotificationStore.         │
│  Realtime: IRealtimeTransport.                                     │
│  Auth: IAuthUserStore + auth ops.                                  │
│  TwoFactor: IOtpChannel.                                           │
│  Support: ISupportStore (يربط Ticket↔Conversation فقط).            │
│  كلّ kit يَجهَل بقيّة الـ kits. أيّ ربط يصير في طبقة Compositions.   │
└────────────────────────────────────────────────────────────────────┘
```

**القاعدة**: kit واحد = مجال محاسبة واحد. تركيبان = طبقة compositions.
ترتيب فوق ترتيب = composition تستهلك composition.

---

## ٣) OAM المُكتَّب (Typed OAM) — لا سلاسل بعد الآن

### ٣-أ. أنواع جديدة

```csharp
namespace ACommerce.OperationEngine.Core;

/// <summary>نوع العمليّة كـ Group.Action — record لمساواة قِيَميّة وtoString نظيف.</summary>
public readonly record struct OperationType(string Group, string Action)
{
    public override string ToString() => $"{Group}.{Action}";
    public static implicit operator string(OperationType t) => t.ToString();
}

/// <summary>مفتاح وسم مع نوع قيمة — يُتيح Tag&lt;T&gt;(key, value) آمناً.</summary>
public abstract record TagKey(string Key);
public sealed record StringTagKey(string Key)        : TagKey(Key);
public sealed record GuidTagKey  (string Key)        : TagKey(Key);
public sealed record IntTagKey   (string Key)        : TagKey(Key);
public sealed record EnumTagKey<TEnum>(string Key)   : TagKey(Key) where TEnum : struct, Enum;

/// <summary>Marker = (مفتاح، قيمة) ثابتان مُعلَنان معاً.</summary>
public readonly record struct Marker(string Key, string Value)
{
    public override string ToString() => $"{Key}={Value}";
}

/// <summary>Party prefix قابل للنوع.</summary>
public readonly record struct PartyKind(string Value)
{
    public static readonly PartyKind User    = new("User");
    public static readonly PartyKind Agent   = new("Agent");
    public static readonly PartyKind System  = new("System");
    public static readonly PartyKind Service = new("Service");
}

public readonly record struct PartyRef(PartyKind Kind, string Id)
{
    public override string ToString() => $"{Kind.Value}:{Id}";
    public static implicit operator string(PartyRef r) => r.ToString();
}
```

### ٣-ب. كل kit يُعلن أنواعه بـ `static readonly`

```csharp
namespace ACommerce.Kits.Chat.Operations;

public static class MessageOps
{
    public static readonly OperationType Send         = new("message", "send");
    public static readonly OperationType MarkRead     = new("message", "mark_read");
    public static readonly OperationType BroadcastEcho = new("message", "broadcast_echo");
}

public static class ChatTags
{
    public static readonly StringTagKey ConversationId = new("conversation_id");
    public static readonly EnumTagKey<MessageOrigin> Origin = new("message_origin");
}

public enum MessageOrigin { User, System, Agent, Bot }
```

### ٣-ج. Builder API يستهلك الأنواع

```csharp
// قبل
Entry.Create("message.send")
    .Tag("conversation_id", id)
    .Tag("kind", "support");

// بعد
Entry.Create(MessageOps.Send)
    .Tag(ChatTags.ConversationId, id)
    .Mark(SupportMarkers.IsTicketReply);
```

`Mark(...)` overload جديد على Builder:
```csharp
public OperationBuilder Mark(Marker m) => Tag(m.Key, m.Value);
```

### ٣-د. Interceptors يستهلكونها

```csharp
public sealed class ChatRealtimeInterceptor : IOperationInterceptor
{
    public string Name => nameof(ChatRealtimeInterceptor);
    public InterceptorPhase Phase => InterceptorPhase.Post;

    // مطابقة بـ OperationType مباشرةً، بدل سلسلة
    public bool AppliesTo(Operation op) => op.Type == MessageOps.Send;

    public Task<AnalyzerResult> InterceptAsync(OperationContext ctx, OperationResult? result)
    {
        // ctx.Tag(ChatTags.ConversationId) typed reader
        ...
    }
}
```

### ٣-هـ. التحقّق الفوريّ في الـ engine

`OperationBuilder.Build()` يفحص أنّ كلّ tag value يطابق نوع المفتاح.
محاولة `Tag(GuidTagKey x, "abc")` → exception بناءً وقت compile-time أو
أوّل run.

> **الفائدة**: typo في "convarsation_id" يفشل compile (لا يُحلّ المرجع).
> Marker مكتَّب يضمن أنّ المرسِل والمستقبِل يوافقان على القيمة الكاملة.

---

## ٤) عناصر التركيب (Composition primitives)

### ٤-أ. `IInterceptorBundle`

```csharp
public interface IInterceptorBundle
{
    string Name { get; }                 // اسم تشخيصيّ
    IEnumerable<IOperationInterceptor> Interceptors { get; }
}
```

### ٤-ب. `ICompositionDescriptor`

```csharp
public interface ICompositionDescriptor
{
    string Name { get; }
    IEnumerable<Type> RequiredKits { get; }    // [typeof(IChatStore), typeof(IRealtimeTransport)]
    IEnumerable<IInterceptorBundle> Bundles { get; }
    IEnumerable<ICompositionDescriptor> Subcompositions { get; }
}
```

### ٤-ج. `services.AddComposition<TDescriptor>()`

```csharp
public static IServiceCollection AddComposition<T>(this IServiceCollection services)
    where T : ICompositionDescriptor, new()
{
    var d = new T();

    // ① تحقّق أنّ الـ kits المطلوبة مسجَّلة
    foreach (var kitType in d.RequiredKits)
        if (services.All(s => s.ServiceType != kitType))
            throw new InvalidOperationException(
                $"Composition '{d.Name}' requires {kitType.Name} — register the kit first.");

    // ② جمِّع interceptors من جميع الـ bundles + الـ subcompositions
    foreach (var bundle in d.Bundles)
        foreach (var i in bundle.Interceptors)
            services.AddSingleton(i);

    foreach (var sub in d.Subcompositions)
        AddCompositionDescriptor(services, sub);

    return services;
}
```

التركيب المسجَّل **عبر AddComposition** — سطر واحد بكلّ القوّة.

### ٤-د. "مثيل" تركيب (instance of composition)

التركيب نفسه يقبل options، فيُسجَّل عدّة مرّات:

```csharp
services.AddComposition<ChatRealtimeBundle>(opts => opts.Channel = "general");
services.AddComposition<ChatRealtimeBundle>(opts => opts.Channel = "support");
```

كلّ مثيل يحقن interceptors تطابق على tag إضافيّ يحدّد الـ channel.
المُنشئ هنا يُنتج "كائناً تركيبيّاً" حيّاً — لا مجرّد ضمّ DI.

---

## ٥) أمثلة عمليّة كاملة

### ٥-أ. Auth + TwoFactor.SMS

#### الـ Kits

- `ACommerce.Kits.Auth.Operations`: `IAuthUserStore`, `LoginOps`, `AuthTags`
  — لا يعرف بـ TwoFactor.
- `ACommerce.Kits.Auth.TwoFactor.Operations`: `IOtpStore`, `IOtpChannel`,
  `OtpOps` — لا يعرف بـ Auth.

#### الـ Composition

```
libs/compositions/Auth.WithSmsOtp/
  ACommerce.Compositions.Auth.WithSmsOtp.csproj
  AuthSmsOtpComposition.cs   ← ICompositionDescriptor
  Bundles/
    OtpRequestBundle.cs       ← interceptor on op.Type == LoginOps.Request
                                 → triggers OtpOps.Send (child operation)
    OtpVerifyBundle.cs        ← interceptor on op.Type == LoginOps.Verify
                                 → child OtpOps.Verify ثمّ يكمل المصادقة
```

```csharp
public sealed class AuthSmsOtpComposition : ICompositionDescriptor
{
    public string Name => "Auth + 2FA via SMS";
    public IEnumerable<Type> RequiredKits => [typeof(IAuthUserStore), typeof(IOtpChannel)];
    public IEnumerable<IInterceptorBundle> Bundles =>
        [new OtpRequestBundle(), new OtpVerifyBundle()];
    public IEnumerable<ICompositionDescriptor> Subcompositions => [];
}
```

#### في التطبيق

```csharp
// Program.cs
builder.Services.AddAuthKit<EjarAuthUserStore>();
builder.Services.AddTwoFactorKit<EjarOtpStore>(opts => opts.Provider = SmsProviders.Mock);
builder.Services.AddComposition<AuthSmsOtpComposition>();
```

ثلاثة أسطر. لا يحقن أحد الكيتَين الآخر، الـ composition هو الجسر.

---

### ٥-ب. Chat + Notifications

#### الـ Kits

- Chat: `IChatStore` — `AppendMessageAsync` تحفظ الرسالة فقط، **بلا**
  broadcast أو FCM أو notification side effects.
- Notifications: `INotificationStore` + `INotificationChannel` (FCM/InApp).
  لا يعرف بـ Chat.

#### الـ Composition

```
libs/compositions/Chat.WithNotifications/
  ChatNotificationsComposition.cs
  Bundles/
    DbNotificationBundle.cs   ← interceptor on MessageOps.Send post-execute
                                 → ينشئ NotificationEntity للمستلم
    PushNotificationBundle.cs ← interceptor on MessageOps.Send post-execute
                                 → يُرسل عبر INotificationChannel
```

```csharp
public sealed class ChatNotificationsComposition : ICompositionDescriptor
{
    public IEnumerable<IInterceptorBundle> Bundles =>
        [new DbNotificationBundle(), new PushNotificationBundle()];
    ...
}
```

#### المعترض الفعليّ

```csharp
public sealed class PushNotificationBundle : IInterceptorBundle
{
    public IEnumerable<IOperationInterceptor> Interceptors => new[]
    {
        new TypedTaggedInterceptor(
            name: "ChatPushOnSend",
            phase: InterceptorPhase.Post,
            opType: MessageOps.Send,
            handler: async (ctx, _) =>
            {
                var convId = ctx.RequireTag(ChatTags.ConversationId);
                var senderId = ctx.RequireFrom().Id;
                // قراءة المستلم من store الدردشة + إرسال FCM
                var chat = ctx.Resolve<IChatStore>();
                var conv = await chat.GetConversationAsync(convId, ctx.CancellationToken);
                var recipient = conv?.OwnerId == senderId ? conv?.PartnerId : conv?.OwnerId;
                if (recipient is { Length: > 0 } && recipient != senderId)
                {
                    var ch = ctx.Resolve<INotificationChannel>();
                    await ch.SendAsync(recipient, ...);
                }
                return AnalyzerResult.Pass();
            })
    };
}
```

`TypedTaggedInterceptor` widget جديد يأخذ `OperationType` بدل سلسلة.

---

### ٥-ج. Chat + Notifications + Realtime

```
libs/compositions/ChatRealtime/
  ChatRealtimeComposition.cs
    Subcompositions: [ChatNotificationsComposition]
    Bundles: [RealtimeBroadcastBundle]
```

```csharp
public sealed class RealtimeBroadcastBundle : IInterceptorBundle
{
    public IEnumerable<IOperationInterceptor> Interceptors => new[]
    {
        new TypedTaggedInterceptor(
            name: "ChatRealtimeOnSend",
            phase: InterceptorPhase.Post,
            opType: MessageOps.Send,
            handler: async (ctx, _) =>
            {
                var transport = ctx.Resolve<IRealtimeTransport>();
                var convId = ctx.RequireTag(ChatTags.ConversationId);
                var msg = ctx.RequireResult<IChatMessage>();
                await transport.SendToUserAsync(msg.SenderPartyId, MessageOps.BroadcastEcho.Action, msg);
                ...
            })
    };
}
```

ChatRealtime يضمّ `ChatNotificationsComposition` كـ subcomposition. مسجّلاً
يضمّ كلّ الـ bundles المتدرّجة. سطر واحد:

```csharp
builder.Services.AddComposition<ChatRealtimeComposition>();
```

---

### ٥-د. Support يَستخدم ChatRealtime — تركيب فوق تركيب

```csharp
public sealed class SupportComposition : ICompositionDescriptor
{
    public IEnumerable<Type> RequiredKits => [typeof(ISupportStore)];
    public IEnumerable<ICompositionDescriptor> Subcompositions => [new ChatRealtimeComposition()];
    public IEnumerable<IInterceptorBundle> Bundles => [new SupportTicketBumpBundle()];
}
```

`SupportTicketBumpBundle` معترض على `MessageOps.Send` يطابق على
`marker(SupportMarkers.IsTicketReply)`. يبحث عن `ticket_id` ويحدِّث
`Ticket.UpdatedAt + LastReplyFromRole`. التركيب الكامل هنا = خمسة kits
+ ثلاث compositions، نقطة دخول واحدة:

```csharp
builder.Services.AddComposition<SupportComposition>();
```

---

## ٦) أين تعيش كلّ composition (قاعدة الحجم)

| الحجم | المكان | السبب |
|---|---|---|
| ≤ 50 سطر إجماليّ | `Program.cs` كـ extension method محلّيّة | لا قيمة من حزمة منفصلة |
| 50-200 سطر | مجلَّد `Compositions/` ضمن مشروع التطبيق | داخل التطبيق، يبقى مرئيّاً |
| > 200 سطر، أو يُستهلَك في تطبيقَين أو أكثر | حزمة في `libs/compositions/X/` | إعادة استخدام صريحة |

> **مثال**: تركيب OTP-via-Email لتطبيق إداريّ صغير = inline في Program.cs.
> تركيب ChatRealtime يستهلكه Ejar.Customer + Ejar.Provider + Ejar.Admin =
> `libs/compositions/Chat.Realtime`.

---

## ٧) التركيب الذاتيّ (recursive composition)

التركيب الناتج هو نفسه observable: للـ subcompositions، الـ bundles،
والـ tags الناتجة. يمكن الكتابة على أيّ منها بـ interceptors خارجيّة.

مثال: تركيب جديد "rate-limit-chat-replies":

```csharp
public sealed class RateLimitedChatComposition : ICompositionDescriptor
{
    public IEnumerable<ICompositionDescriptor> Subcompositions => [new ChatRealtimeComposition()];
    public IEnumerable<IInterceptorBundle> Bundles => [new RateLimitBundle()];
}

public sealed class RateLimitBundle : IInterceptorBundle
{
    public IEnumerable<IOperationInterceptor> Interceptors => new[]
    {
        new TypedTaggedInterceptor(
            name: "RateLimit",
            phase: InterceptorPhase.Pre,    // قبل التنفيذ
            opType: MessageOps.Send,
            handler: async (ctx, _) =>
            {
                var sender = ctx.RequireFrom();
                var bucket = ctx.Resolve<IRateLimiter>();
                if (!await bucket.AllowAsync($"chat:{sender}", limitPerMinute: 30))
                    return AnalyzerResult.Fail("rate_limited");
                return AnalyzerResult.Pass();
            })
    };
}
```

التركيب يعمل فوق ChatRealtime (الذي يعمل فوق ChatNotifications). كلّ
طبقة تضيف سلوكاً بدون أن تعرف الأخرى.

---

## ٨) أين توضع الأنواع — مرجعيّة

| النوع | المكان | السبب |
|---|---|---|
| `OperationType` المعياريّة | `ACommerce.OperationEngine.Core` | core engine يلزمه |
| `OperationType` لكيت | `<Kit>.Operations/Operations/<KitOps>.cs` | تعريف الـ kit نفسه |
| `Marker`/`TagKey` لكيت | `<Kit>.Operations/Operations/<KitTags>.cs` | نفس الـ kit |
| `IInterceptorBundle` لتركيب | `Compositions/<X>/Bundles/<Y>Bundle.cs` | لا يخرج التركيب |
| `ICompositionDescriptor` | `Compositions/<X>/<X>Composition.cs` | عنوان التركيب |
| Marker مشترَك بين kits (نادر) | `libs/kits/Core/SharedMarkers/` | الكيتس لا تعرف بعضها، لكن TYPES مشتركة |

---

## ٩) الفروقات عن الموجود — ما يتغيّر

| اليوم | المستهدَف |
|---|---|
| `Entry.Create("message.send")` | `Entry.Create(MessageOps.Send)` |
| `op.Type == "message.send"` في interceptor | `op.Type == MessageOps.Send` (record equality) |
| `services.AddSingleton<IOperationInterceptor>(...)` لكلّ معترض | `services.AddComposition<X>()` |
| `EjarCustomerChatStore.AppendMessageAsync` يبثّ ويُنشِئ notification ويُرسل FCM | الـ store يحفظ فقط؛ Bundle interceptors تتولّى الباقي |
| Chat kit يحقن `IRealtimeTransport` | Chat kit لا يعرف بـ realtime |
| Support kit يحقن `IChatStore` للردود | Support يبقى — `IChatStore` interface مقبول كمحاسبة-عابرة. لكنّ side effects (broadcast/FCM) تأتي من interceptors لا من store |
| سلاسل سحريّة في Tags | `static readonly` types فقط |

---

## ١٠) تعديلات لازمة على core engine (إن لزم)

### ١٠-أ. `OperationContext.RequireTag<T>(TagKey<T>)` typed accessor
موجود اليوم `Get<T>(string)` — نضيف overload يتقبّل `TagKey<T>` ويردّ `T`.

### ١٠-ب. `OperationContext.RequireResult<T>()`
نتيجة الـ Execute body كانت تُكتَب في `ctx.Set("db_result", obj)`. نُضيف
typed wrapper.

### ١٠-ج. Builder `.Mark(Marker)` overload
سطر سهل (راجع §٣-ج).

### ١٠-د. `TypedTaggedInterceptor` ك helper
لا يلزم تغيير `IOperationInterceptor` — مجرّد widget يأخذ
`OperationType` typed بدل سلسلة.

### ١٠-هـ. Diagnostics: AnalyzeOnly mode
لـ debugging compositions، خيار يطبع كلّ interceptor يطابق على عمليّة،
ترتيب التنفيذ، النتيجة. يساعد على تشخيص "لماذا interceptor لم يعمل".

---

## ١١) المراحل (خارطة طريق)

> يُنفَّذ على دفعات. لا تجريف الكود الحاليّ في PR واحد.

### المرحلة A — typed OAM فقط (PR صغير)
- إضافة `OperationType`/`Marker`/`TagKey`/`PartyKind`/`PartyRef` إلى core.
- `Entry.Create` overload يقبل `OperationType`.
- `Tag(TagKey<T>, T)` typed overloads.
- توافق كامل مع الكود القديم (سلاسل تبقى تعمل عبر implicit conversion).
- لا تغيير في kits بعد.

### المرحلة B — Chat kit نقيّ (PR متوسط)
- لقطة `EjarCustomerChatStore.AppendMessageAsync`: تُحذَف منها broadcast +
  FCM + DB notification side effects.
- إنشاء `libs/compositions/Chat.WithNotifications` و `libs/compositions/Chat.Realtime`.
- Ejar Program.cs يصبح: `AddChatKit + AddNotificationsKit + AddRealtimeKit
  + AddComposition<ChatRealtimeComposition>()`.
- اختبارات realtime/notification existing تبقى تعمل (نفس السلوك من
  الخارج).

### المرحلة C — Auth + 2FA composition (PR صغير)
- لا تغيير على Auth أو TwoFactor kits.
- إنشاء `libs/compositions/Auth.WithSmsOtp` يضمّهما.
- Ejar.Api يستخدم `AddComposition<AuthSmsOtpComposition>()`.

### المرحلة D — Support يصبح subcomposition من ChatRealtime (PR صغير)
- `EjarSupportStore.OpenAsync` يبقى ينشئ Conversation + Ticket. لكنّ
  الـ side effects الإضافيّة المتعلقة بالتذاكر (تحديث `Ticket.UpdatedAt`،
  إشعار "تذكرة جديدة" لفريق الدعم) تنتقل إلى `SupportTicketBumpBundle`.
- `SupportComposition` تستخدم `ChatRealtimeComposition` subcomposition.
- Ejar.Api: `AddComposition<SupportComposition>()` بدل `AddComposition
  <ChatRealtimeComposition>() + AddSupportKit(...)`.

### المرحلة E — تنظيف
- إزالة الـ string overloads القديمة من builders (deprecated → removed).
- إزالة const string ثوابت kits (تحلّ محلّها typed objects).
- توحيد PartyKind في كلّ التطبيقات على `PartyKind.User`.

---

## ١٢) لماذا هذا أفضل (الفائدة المرجوّة)

١. **خطأ كتابيّ → خطأ بناء**: typo في `MessageOps.Send` يفشل compile،
   typo في `"message.send"` يفشل في الإنتاج بصمت.
٢. **kits قابلة للاستخدام بدون تركيب**: تطبيق صغير يستخدم Chat kit بلا
   Notifications أو Realtime — ينجح بلا تعديل، فقط بدون interceptors.
٣. **التركيب وحدة قابلة للاستبدال**: استبدال `ChatRealtimeComposition`
   بتركيب آخر (مثل `ChatPolling` بدل realtime) لا يلمس Chat kit.
٤. **التركيب يَتَكدَّس**: rate-limit + audit + chat-realtime + support — كلّ
   منها composition، يضمّها التطبيق في `AddComposition<App>()`.
٥. **التحقّق وقت الإقلاع**: `RequiredKits` يكشف نقص اعتماد قبل أوّل طلب.
٦. **تشخيص أبسط**: bug في "الردّ لا يصل" — تتبَّع interceptors المشتركة
   على `MessageOps.Send`، اعرف أيّها فشل، صلِّح.
٧. **تقليل سطح الخطأ**: كلّ ربط بين kits يحدث في مكان واحد (Bundle)،
   موثّق، مفحوص.

---

## ١٣) ما لا يجب أن نفعله

- ❌ نقل entities من kit إلى آخر — التركيب يمرّ على interfaces فقط.
- ❌ wide store interface تخدم composition — الـ store يخدم الـ kit الذي
  وُلد فيه. لو composition تحتاج بيانات kit آخر، تستهلكها عبر
  `ctx.Resolve<...>()` في الـ interceptor.
- ❌ composition تستهلك أكثر من ٤ kits مباشرة — قسّمها إلى compositions
  متدرّجة.
- ❌ صفّ ثوابت من نوع `static class FooStrings { public const string ... }`
  للـ tags. كلّ شيء `static readonly TagKey<T>` أو `Marker`.

---

## ١٤) قياس النجاح

عند اكتمال جميع المراحل:
- `grep -r '"message.send"' libs Apps` يُرجع ٠ نتائج (كلّها صارت
  `MessageOps.Send`).
- `grep -r 'AddSingleton<IOperationInterceptor>' Apps` يُرجع ٠ نتائج
  (كلّها صارت `AddComposition<...>()`).
- إضافة kit جديد لتطبيق = ٣ أسطر (kit + composition + AddComposition).
- إزالة kit = حذف سطر AddComposition، البقية تنظف نفسها.

---

## ملاحظة — هذه خطّة، ليست تنفيذاً

أيّ مرحلة تبدأ في PR منفصل، بحجم محدود، ومع اختبارات موازية. التنفيذ
الفوريّ كاملاً يكسر الكثير في وقت واحد ويُصعّب المراجعة.

> راجع `docs/PITFALLS.md` لتذكر فخاخ التطبيق السابقة. راجع `CLAUDE.md`
> لقواعد الـ session. هذه الوثيقة الثالثة المكمِّلة لهما — اقرأها قبل
> البدء بأيّ مرحلة من المراحل أعلاه.
