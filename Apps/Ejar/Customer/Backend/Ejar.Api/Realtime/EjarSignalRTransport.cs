using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Ejar.Api.Realtime;

/// <summary>
/// IRealtimeTransport يربط <see cref="EjarRealtimeHub"/> الفعليّ بمنطق الكيت
/// (<c>ChatService.BroadcastNewMessageAsync</c> وغيره). الكيت يأتي بنسخة
/// <c>SignalRRealtimeTransport</c> مربوطة بـ <c>AShareHub</c> ثابتاً، لكن
/// تطبيقنا يخدم <c>EjarRealtimeHub : AShareHub</c> على <c>/realtime</c>،
/// و SignalR يفرز groups لكلّ نوع Hub منفرداً.
///
/// <para><b>الأثر إن لم نستبدل</b>: <c>OpenAsync(userId, connId, "notif:conv:X")</c>
/// يستدعي <c>IHubContext&lt;AShareHub&gt;.Groups.AddToGroupAsync</c> فيُضاف
/// connId إلى group على AShareHub — وهو نوع Hub لا توجد فيه أيّ connections
/// (لأنّ كلّ الاتصالات مفتوحة على EjarRealtimeHub). كلّ <c>SendToGroupAsync</c>
/// يبثّ إلى group فارغة، فلا يصل أحد. هذه القشرة ترفع IHubContext إلى
/// <see cref="EjarRealtimeHub"/> الفعليّ فيُحلّ المشكل.</para>
/// </summary>
public sealed class EjarSignalRTransport : IRealtimeTransport
{
    private readonly IHubContext<EjarRealtimeHub> _hub;

    public EjarSignalRTransport(IHubContext<EjarRealtimeHub> hub) => _hub = hub;

    public Task SendToUserAsync(string userId, string method, object data, CancellationToken ct = default)
        => _hub.Clients.User(userId).SendAsync(method, data, ct);

    public Task SendToGroupAsync(string groupName, string method, object data, CancellationToken ct = default)
        => _hub.Clients.Group(groupName).SendAsync(method, data, ct);

    public Task BroadcastAsync(string method, object data, CancellationToken ct = default)
        => _hub.Clients.All.SendAsync(method, data, ct);

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
        => _hub.Groups.AddToGroupAsync(connectionId, groupName, ct);

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
        => _hub.Groups.RemoveFromGroupAsync(connectionId, groupName, ct);
}
