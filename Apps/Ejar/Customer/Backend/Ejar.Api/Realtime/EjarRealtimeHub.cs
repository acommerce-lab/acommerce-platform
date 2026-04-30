using ACommerce.Chat.Operations;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.SignalR;
using Ejar.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Realtime;

/// <summary>
/// Hub خاصّ بإيجار يرث AShareHub. على كلّ اتصال جديد يشترك المستخدم تلقائياً
/// بقنوات <c>notif:conv:{X}</c> لكلّ المحادثات التي يشارك فيها (Owner أو
/// Partner). بدون هذا الاشتراك، <see cref="IChatService.BroadcastNewMessageAsync"/>
/// يبثّ على قناة لا يستمع إليها أحد فلا يصل أيّ إشعار للطرف الآخر.
///
/// <para><b>دورة الحياة الصحيحة:</b>
/// <list type="number">
///   <item>المستخدم يفتح التطبيق → SignalR connect → <c>OnConnectedAsync</c></item>
///   <item>هنا نُسجّله في <c>notif:conv:X</c> لكلّ محادثة يشارك فيها.</item>
///   <item>عند فتحه ChatRoom → POST /chat/{id}/enter → يفتح <c>chat:conv:X</c>،
///         <see cref="ChatExtensions.WireChatNotificationCoupling"/> يقفل
///         <c>notif:conv:X</c> له لتلك المحادثة.</item>
///   <item>عند مغادرته ChatRoom → POST /chat/{id}/leave → يقفل <c>chat:conv:X</c>،
///         الـ Coupling يعيد فتح <c>notif:conv:X</c>.</item>
/// </list></para>
/// </summary>
public sealed class EjarRealtimeHub : AShareHub
{
    private readonly EjarDbContext _db;
    private readonly IChatService _chat;

    public EjarRealtimeHub(
        EjarDbContext db,
        IChatService chat,
        IRealtimeChannelManager? channels = null,
        IConnectionTracker? tracker = null)
        : base(channels, tracker)
    {
        _db   = db;
        _chat = chat;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        if (Context.UserIdentifier is not { } userId) return;
        if (!Guid.TryParse(userId, out var uid))      return;

        // اشترك في كلّ notif:conv:X لمحادثات هذا المستخدم. مكلفة فقط مرّة لكلّ
        // اتصال — على هاتف عاديّ بعشرات المحادثات لا يتجاوز ميلّيثانية.
        var convIds = await _db.Conversations.AsNoTracking()
            .Where(c => c.OwnerId == uid || c.PartnerId == uid)
            .Select(c => c.Id)
            .ToListAsync();

        foreach (var convId in convIds)
        {
            try
            {
                await _chat.SubscribeUserAsync(
                    convId.ToString(), userId, Context.ConnectionId);
            }
            catch
            {
                // لا تكسر الاتصال لو فشل اشتراك واحد — حاول الباقي.
            }
        }
    }
}
