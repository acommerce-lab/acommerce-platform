using ACommerce.Chat.Operations;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.SignalR;
using Ejar.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Realtime;

/// <summary>
/// Hub خاصّ بإيجار يرث AShareHub. على كلّ اتصال جديد يشترك المستخدم تلقائياً
/// بقنوات <c>notif:conv:{X}</c> لكلّ المحادثات التي يشارك فيها (Owner أو
/// Partner). بدونه <see cref="IChatService.BroadcastNewMessageAsync"/>
/// يبثّ على قناة لا يستمع إليها أحد.
///
/// <para><b>تنسيق المعرّف</b>: AShareHub يخزّن في <see cref="IConnectionTracker"/>
/// بـ <c>Context.UserIdentifier</c> = الـ guid الخامّ. لكن
/// <c>ChatController</c> في الـ kit يبحث بـ <c>"User:{guid}"</c> (PartyKind +
/// id). فلا يجد connId، وَ <c>POST /chat/{id}/enter</c> يردّ
/// <c>{ ok: false, reason: "no_connection" }</c> بصمت — وَ chat:conv:X لا
/// يُفتح، فالطرف يبقى يعتمد على notif:conv:X فقط (لا توجد رسالة حيّة في غرفة
/// الدردشة).
///
/// لتوحيد السلوك: نتجاوز <c>base.OnConnectedAsync</c> ونتتبّع ونشترك بـ
/// <c>"User:{guid}"</c> صراحةً، فيتطابق مع <c>CallerPartyId</c> في الـ kit.
/// كذلك نُغلق كلّ القنوات لـ partyId عند الانفصال.</para>
/// </summary>
public sealed class EjarRealtimeHub : AShareHub
{
    private const string PartyKind = "User";

    private readonly EjarDbContext _db;
    private readonly IChatService _chat;
    private readonly IRealtimeChannelManager? _channels;
    private readonly IConnectionTracker? _tracker;

    public EjarRealtimeHub(
        EjarDbContext db,
        IChatService chat,
        IRealtimeChannelManager? channels = null,
        IConnectionTracker? tracker = null)
        : base(channels, tracker)
    {
        _db       = db;
        _chat     = chat;
        _channels = channels;
        _tracker  = tracker;
    }

    private static string PartyOf(string userId) => $"{PartyKind}:{userId}";

    public override async Task OnConnectedAsync()
    {
        // لا نستدعي base — يخزّن tracker بـ raw userId. نخزّنه بـ partyId
        // ليتطابق مع ما يبحث عنه ChatController.Enter في الـ kit.
        if (Context.UserIdentifier is not { } userId)
        {
            await base.OnConnectedAsync();
            return;
        }

        if (_tracker is not null)
            await _tracker.TrackConnectionAsync(PartyOf(userId), Context.ConnectionId);

        if (!Guid.TryParse(userId, out var uid)) return;

        // اشترك في كلّ notif:conv:X لمحادثات هذا المستخدم — بمفتاح partyId
        // ليتسق مع ChatController.EnterConversationAsync الذي يستعمل partyId.
        var convIds = await _db.Conversations.AsNoTracking()
            .Where(c => c.OwnerId == uid || c.PartnerId == uid)
            .Select(c => c.Id)
            .ToListAsync();

        foreach (var convId in convIds)
        {
            try
            {
                await _chat.SubscribeUserAsync(
                    convId.ToString(), PartyOf(userId), Context.ConnectionId);
            }
            catch
            {
                // لا تكسر الاتصال لو فشل اشتراك واحد — حاول الباقي.
            }
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.UserIdentifier is { } userId)
        {
            // نظّف بـ partyId (نفس مفتاح OnConnectedAsync).
            if (_channels is not null)
                await _channels.CloseAllForConnectionAsync(PartyOf(userId), Context.ConnectionId);
            if (_tracker is not null)
                await _tracker.RemoveConnectionAsync(PartyOf(userId));
        }
        // لا نستدعي base — الذي ينظّف بـ raw userId، يكرّر العمل بدون نتيجة.
    }
}
