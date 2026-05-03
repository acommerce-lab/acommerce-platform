using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace ACommerce.Realtime.Providers.SignalR;

/// <summary>
/// Minimal SignalR hub for realtime routing. Wires connection lifecycle into
/// <see cref="IConnectionTracker"/> (userId → connectionId lookup, used by app
/// controllers to address realtime sends) and <see cref="IRealtimeChannelManager"/>
/// (per-user channel subscriptions close on disconnect, firing OnChannelClosed
/// handlers so app-level coupling — like re-opening notif channels — runs).
/// </summary>
public class AShareHub : Hub
{
    private readonly IRealtimeChannelManager? _channels;
    private readonly IConnectionTracker? _tracker;

    public AShareHub(
        IRealtimeChannelManager? channels = null,
        IConnectionTracker? tracker = null)
    {
        _channels = channels;
        _tracker  = tracker;
    }

    public override async Task OnConnectedAsync()
    {
        if (_tracker is not null && Context.UserIdentifier is { } userId)
            await _tracker.TrackConnectionAsync(userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.UserIdentifier is { } userId)
        {
            if (_channels is not null)
                await _channels.CloseAllForConnectionAsync(userId, Context.ConnectionId);
            if (_tracker is not null)
                await _tracker.RemoveConnectionAsync(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
