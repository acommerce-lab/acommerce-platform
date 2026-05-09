using ACommerce.Compositions.Chat.Realtime;
using ACommerce.Compositions.Core;
using ACommerce.Kits.Support.Operations;

namespace ACommerce.Compositions.Support;

/// <summary>
/// تركيب: <c>Support + ChatRealtime</c>. يستخدم
/// <see cref="ChatRealtimeComposition"/> كـ subcomposition (فيُورِّث كلّ
/// interceptors البثّ على رسائل التذاكر تلقائياً) ويُضيف فوقه bundle خاصّ
/// بمتابعة ميتا التذاكر.
///
/// <para>مثال "تركيب فوق تركيب": Support يضمّ Chat.Realtime، الذي بدوره
/// يضمّ Chat + Realtime kits. سطر واحد <c>AddComposition&lt;SupportComposition&gt;()</c>
/// يجلب الكلّ.</para>
///
/// <para>الاستهلاك:
/// <code>
/// services.AddChatKit&lt;EjarCustomerChatStore&gt;();
/// services.AddSignalR... // أو أيّ realtime provider
/// services.AddSupportKit&lt;EjarSupportStore&gt;(...);
/// services.AddComposition&lt;SupportComposition&gt;();
/// // لا حاجة لـ AddComposition&lt;ChatRealtimeComposition&gt;()
/// // — الـ subcomposition يُسجَّل تلقائياً.
/// </code></para>
/// </summary>
public sealed class SupportComposition : ICompositionDescriptor
{
    public string Name => "Support over Chat.Realtime";

    public IEnumerable<Type> RequiredKits => new[]
    {
        typeof(ISupportStore),
    };

    public IEnumerable<ICompositionDescriptor> Subcompositions => new[]
    {
        (ICompositionDescriptor)new ChatRealtimeComposition(),
    };

    public IEnumerable<IInterceptorBundle> Bundles => new[]
    {
        (IInterceptorBundle)new SupportTicketBumpBundle(),
    };
}
