using ACommerce.OperationEngine.Core;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Operations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Realtime.Operations;

public class RealtimeService
{
    private readonly IRealtimeTransport _transport;
    private readonly IConnectionTracker? _tracker;
    private readonly OpEngine _engine;

    public RealtimeService(IRealtimeTransport transport, OpEngine engine, IConnectionTracker? tracker = null)
    {
        _transport = transport;
        _engine = engine;
        _tracker = tracker;
    }

    public Task<OperationResult> SendToUserAsync(PartyId recipient, string method, object data,
        PartyId? sender = null, CancellationToken ct = default)
        => _engine.ExecuteAsync(ConnectionOps.SendToUser(recipient, method, data, _transport, sender), ct);

    public Task<OperationResult> BroadcastAsync(string method, object data, CancellationToken ct = default)
        => _engine.ExecuteAsync(ConnectionOps.Broadcast(method, data, _transport), ct);

    public Task<OperationResult> OnConnectedAsync(PartyId user, string connectionId, CancellationToken ct = default)
        => _engine.ExecuteAsync(ConnectionOps.Connect(user, connectionId, _tracker), ct);

    public Task<OperationResult> OnDisconnectedAsync(PartyId user, CancellationToken ct = default)
        => _engine.ExecuteAsync(ConnectionOps.Disconnect(user, _tracker), ct);

    public Task<OperationResult> JoinGroupAsync(PartyId user, string connectionId, PartyId group, CancellationToken ct = default)
        => _engine.ExecuteAsync(ConnectionOps.JoinGroup(user, connectionId, group, _transport), ct);

    public Task<OperationResult> LeaveGroupAsync(PartyId user, string connectionId, PartyId group, CancellationToken ct = default)
        => _engine.ExecuteAsync(ConnectionOps.LeaveGroup(user, connectionId, group, _transport), ct);

    public Task<bool> IsOnlineAsync(PartyId user, CancellationToken ct = default)
        => _tracker?.IsOnlineAsync(user.Id, ct) ?? Task.FromResult(false);
}

public static class RealtimeExtensions
{
    public static IServiceCollection AddRealtime<TTransport>(this IServiceCollection services)
        where TTransport : class, IRealtimeTransport
    {
        services.AddScoped<IRealtimeTransport, TTransport>();
        services.AddScoped<RealtimeService>();
        return services;
    }

    public static IServiceCollection AddRealtime(this IServiceCollection services, IRealtimeTransport transport)
    {
        services.AddSingleton(transport);
        services.AddScoped<RealtimeService>();
        return services;
    }

    public static IServiceCollection AddConnectionTracker<TTracker>(this IServiceCollection services)
        where TTracker : class, IConnectionTracker
    {
        services.AddScoped<IConnectionTracker, TTracker>();
        return services;
    }
}
