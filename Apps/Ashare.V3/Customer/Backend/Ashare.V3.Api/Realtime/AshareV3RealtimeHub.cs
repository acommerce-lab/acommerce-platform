using ACommerce.Chat.Operations;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.SignalR;
using Ashare.V3.Data;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Realtime;

/// <summary>
/// Hub خاصّ بِعَشير V3 يَرِث AShareHub. عَلى كُلّ اتِّصال جَديد يَشتَرِك
/// المُستَخدِم تِلقائيّاً بِقَنَوات <c>notif:conv:{X}</c> لِكُلّ المُحادَثات
/// الَّتي يُشارِك فيها (عَبر <see cref="ChatParticipantEntity"/>).
///
/// <para><b>تَنسيق المُعَرِّف</b>: AShareHub يَخزِن في
/// <see cref="IConnectionTracker"/> بِـ <c>Context.UserIdentifier</c> = الـ id
/// الخامّ. لكنّ <c>ChatController</c> في الـ kit يَبحَث بِـ <c>"User:{id}"</c>
/// (PartyKind + id). نَتَجاوَز <c>base.OnConnectedAsync</c> ونَتَتَبَّع
/// ونَشتَرِك بِـ <c>"User:{id}"</c> صَراحَةً.</para>
///
/// <para><b>ملاحَظَة V3</b>: asharedb يَستَخدِم نَموذَج
/// <c>Chat</c>/<c>ChatParticipant</c> (m:n) — يَختَلِف عَن نَموذَج
/// <c>Conversation</c> الثُنائي في إيجار. الاستِعلام هُنا يَجمَع كُلّ
/// chatId يُشارِك فيها المُستَخدِم.</para>
/// </summary>
public sealed class AshareV3RealtimeHub : AShareHub
{
    private const string PartyKind = "User";

    private readonly AshareV3DbContext _db;
    private readonly IChatService _chat;
    private readonly IRealtimeChannelManager? _channels;
    private readonly IConnectionTracker? _tracker;

    public AshareV3RealtimeHub(
        AshareV3DbContext db,
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
        // لا نَستَدعي base — يَخزِن tracker بِـ raw userId. نَخزِنه بِـ partyId
        // لِيَتَطابَق مَع ما يَبحَث عَنه ChatController.Enter في الـ kit.
        if (Context.UserIdentifier is not { } userId)
        {
            await base.OnConnectedAsync();
            return;
        }

        if (_tracker is not null)
            await _tracker.TrackConnectionAsync(PartyOf(userId), Context.ConnectionId);

        // اشتَرِك في كُلّ notif:conv:X لِمُحادَثات هذا المُستَخدِم — بِمِفتاح
        // partyId لِيَتَّسِق مَع ChatController.EnterConversationAsync.
        var convIds = await _db.ChatParticipants.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ChatId)
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
                // لا تَكسِر الاتِّصال لَو فَشِل اشتِراك واحِد — حاوِل الباقي.
            }
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.UserIdentifier is { } userId)
        {
            if (_channels is not null)
                await _channels.CloseAllForConnectionAsync(PartyOf(userId), Context.ConnectionId);
            if (_tracker is not null)
                await _tracker.RemoveConnectionAsync(PartyOf(userId));
        }
        // لا نَستَدعي base — الَّذي يُنَظِّف بِـ raw userId، يُكَرِّر العَمَل بِدون نَتيجَة.
    }
}
