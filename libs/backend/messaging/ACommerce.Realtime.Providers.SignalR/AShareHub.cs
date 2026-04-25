using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace ACommerce.Realtime.Providers.SignalR;

/// <summary>
/// Minimal SignalR hub for realtime routing. Forwards disconnects to
/// <see cref="IRealtimeChannelManager"/> so any open per-user channels close
/// cleanly (firing OnChannelClosed handlers).
/// </summary>
public class AShareHub : Hub
{
    private readonly IRealtimeChannelManager? _channels;

    public AShareHub(IRealtimeChannelManager? channels = null)
    {
        _channels = channels;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_channels is not null && Context.UserIdentifier is { } userId)
            await _channels.CloseAllForConnectionAsync(userId, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
