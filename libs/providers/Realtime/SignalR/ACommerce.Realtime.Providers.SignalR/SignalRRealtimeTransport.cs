using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace ACommerce.Realtime.Providers.SignalR;

/// <summary>Production SignalR implementation of IRealtimeTransport.</summary>
public class SignalRRealtimeTransport : IRealtimeTransport
{
    private readonly IHubContext<AShareHub> _hub;

    public SignalRRealtimeTransport(IHubContext<AShareHub> hub)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
    }

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
