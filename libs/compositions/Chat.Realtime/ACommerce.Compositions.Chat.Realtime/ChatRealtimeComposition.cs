using ACommerce.Compositions.Core;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Realtime.Operations.Abstractions;

namespace ACommerce.Compositions.Chat.Realtime;

/// <summary>
/// تركيب: <c>Chat + Realtime</c>. يجعل كلّ <see cref="ChatOps.MessageSend"/>
/// تبثّ تلقائياً للمرسِل والمستلم بدون أيّ تعديل في Chat kit أو
/// Realtime kit. الـ kits يبقيان نقيَّين.
///
/// <para>الاستهلاك:
/// <code>
/// services.AddChatKit&lt;EjarCustomerChatStore&gt;();
/// services.AddSignalRRealtimeKit(...);  // أو أيّ provider
/// services.AddComposition&lt;ChatRealtimeComposition&gt;();
/// </code></para>
/// </summary>
public sealed class ChatRealtimeComposition : ICompositionDescriptor
{
    public string Name => "Chat + Realtime";

    public IEnumerable<Type> RequiredKits => new[]
    {
        typeof(IChatStore),
        typeof(IRealtimeTransport),
    };

    public IEnumerable<IInterceptorBundle> Bundles => new[]
    {
        (IInterceptorBundle)new RealtimeBroadcastBundle(),
    };
}
