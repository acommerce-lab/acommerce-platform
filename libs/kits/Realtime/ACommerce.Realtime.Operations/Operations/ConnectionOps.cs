using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.Realtime.Operations.Abstractions;

namespace ACommerce.Realtime.Operations.Operations;

/// <summary>
/// قيود الاتصال - كل متغير مكتوب (typed).
/// لا نصوص حرة في أي دالة عامة.
/// </summary>
public static class ConnectionOps
{
    public static Operation Connect(PartyId user, string connectionId, IConnectionTracker? tracker = null)
    {
        return Entry.Create("realtime.connect")
            .Describe($"{user} connected")
            .From(user, 1, (RT.ConnectionId, connectionId))
            .To(PartyId.System, 1, (RT.Role, "host"))
            .Tag(RT.Presence, PresenceStatus.Online)
            .Execute(async ctx =>
            {
                if (tracker != null)
                    await tracker.TrackConnectionAsync(user.Id, connectionId, ctx.CancellationToken);
                ctx.Set("userId", user.Id);
                ctx.Set("connectionId", connectionId);
                ctx.Set("connectedAt", DateTime.UtcNow);
            })
            .Build();
    }

    public static Operation Disconnect(PartyId user, IConnectionTracker? tracker = null)
    {
        return Entry.Create("realtime.disconnect")
            .Describe($"{user} disconnected")
            .From(PartyId.System, 1)
            .To(user, 1)
            .Tag(RT.Presence, PresenceStatus.Offline)
            .Execute(async ctx =>
            {
                if (tracker != null)
                    await tracker.RemoveConnectionAsync(user.Id, ctx.CancellationToken);
                ctx.Set("disconnectedAt", DateTime.UtcNow);
            })
            .Build();
    }

    public static Operation JoinGroup(PartyId user, string connectionId, PartyId group,
        IRealtimeTransport transport)
    {
        return Entry.Create("realtime.join_group")
            .Describe($"{user} joins {group}")
            .From(user, 1, (RT.ConnectionId, connectionId))
            .To(group, 1, (RT.Group, group.Id))
            .Execute(async ctx =>
            {
                await transport.AddToGroupAsync(connectionId, group.Id, ctx.CancellationToken);
                ctx.Set("groupId", group.Id);
            })
            .Build();
    }

    public static Operation LeaveGroup(PartyId user, string connectionId, PartyId group,
        IRealtimeTransport transport)
    {
        return Entry.Create("realtime.leave_group")
            .From(group, 1, (RT.Group, group.Id))
            .To(user, 1)
            .Execute(async ctx =>
            {
                await transport.RemoveFromGroupAsync(connectionId, group.Id, ctx.CancellationToken);
            })
            .Build();
    }

    public static Operation SendToUser(PartyId recipient, string method, object data,
        IRealtimeTransport transport, PartyId? sender = null)
    {
        return Entry.Create("realtime.send")
            .Describe($"Send {method} to {recipient}")
            .From(sender ?? PartyId.System, 1, (RT.Role, "sender"))
            .To(recipient, 1, (RT.Role, "recipient"), (RT.Delivery, DeliveryStatus.Pending))
            .Tag(RT.Method, method)
            .Execute(async ctx =>
            {
                await transport.SendToUserAsync(recipient.Id, method, data, ctx.CancellationToken);
                var r = ctx.Operation.GetPartiesByTag(RT.Role, "recipient").FirstOrDefault();
                if (r != null) { r.RemoveTag(RT.Delivery); r.AddTag(RT.Delivery, DeliveryStatus.Sent); }
                ctx.Set("delivered", true);
            })
            .Build();
    }

    public static Operation Broadcast(string method, object data, IRealtimeTransport transport)
    {
        return Entry.Create("realtime.broadcast")
            .From(PartyId.System, 1)
            .To(PartyId.All, 1)
            .Tag(RT.Method, method)
            .Execute(async ctx => await transport.BroadcastAsync(method, data, ctx.CancellationToken))
            .Build();
    }
}
