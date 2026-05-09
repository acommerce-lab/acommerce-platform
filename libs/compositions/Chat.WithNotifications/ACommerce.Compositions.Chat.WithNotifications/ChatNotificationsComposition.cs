using ACommerce.Compositions.Core;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Notifications.Backend;

namespace ACommerce.Compositions.Chat.WithNotifications;

/// <summary>
/// تركيب: <c>Chat + Notifications</c>. لكلّ <c>message.send</c> يحدث:
/// <list type="number">
///   <item>سجلّ إشعار في DB (<see cref="ChatPersistentNotificationBundle"/>)
///         فيُرى في صفحة /notifications حتى عند فقد الجلسة الحيّة.</item>
///   <item>إرسال push (FCM/Web Push) عبر
///         <see cref="ChatPushNotificationBundle"/> لو سُجِّل
///         <c>INotificationChannel</c> — وإلاّ يُتجاوز بصمت.</item>
/// </list>
/// لا Chat kit ولا Notifications kit يعرف الآخر. لو أُسقطت هذه الـ
/// composition، Chat لا يزال يحفظ الرسائل لكن لا تُنشأ إشعارات — اختيار
/// نظيف.
/// </summary>
public sealed class ChatNotificationsComposition : ICompositionDescriptor
{
    public string Name => "Chat + Notifications (DB record + Push)";

    public IEnumerable<Type> RequiredKits => new[]
    {
        typeof(IChatStore),
        typeof(INotificationStore),
    };

    public IEnumerable<IInterceptorBundle> Bundles => new IInterceptorBundle[]
    {
        new ChatPersistentNotificationBundle(),
        new ChatPushNotificationBundle(),
    };
}
