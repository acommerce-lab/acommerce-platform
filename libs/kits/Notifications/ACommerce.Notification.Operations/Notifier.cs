using ACommerce.Notification.Operations.Abstractions;
using ACommerce.Notification.Operations.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Notification.Operations;

/// <summary>
/// تهيئة الإشعارات.
///
/// services.AddNotifications(config => {
///     config.AddChannel(new InAppChannel(transport));
///     config.AddChannel(new FirebaseChannel(key));
///     config.DefineType(AppNotifications.NewOrder);
///     config.DefineType(AppNotifications.Marketing);
/// });
/// </summary>
public class NotificationConfig
{
    internal Dictionary<string, INotificationChannel> Channels { get; } = new();
    internal Dictionary<string, NotificationType> Types { get; } = new();

    public NotificationConfig AddChannel(INotificationChannel channel)
    {
        Channels[channel.ChannelName] = channel;
        return this;
    }

    /// <summary>
    /// تسجيل نوع إشعار مُعرّف مسبقاً.
    /// المطور يُنشئ NotificationType ككائن في تطبيقه.
    /// </summary>
    public NotificationConfig DefineType(NotificationType type)
    {
        Types[type.Name] = type;
        return this;
    }
}

/// <summary>
/// واجهة المطور البسيطة.
///
///   await notifier.SendAsync(AppNotifications.NewOrder, PartyId.User("123"), data);
/// </summary>
public class Notifier
{
    private readonly NotificationConfig _config;
    private readonly OpEngine _engine;

    public Notifier(NotificationConfig config, OpEngine engine)
    {
        _config = config;
        _engine = engine;
    }

    /// <summary>
    /// إرسال إشعار بنوعه. القنوات والأولوية تُحدد من NotificationType.
    /// </summary>
    public async Task<OperationResult> SendAsync(
        NotificationType type,
        PartyId recipient,
        object? data = null,
        string? titleOverride = null,
        string? messageOverride = null,
        Func<OperationContext, Task>? afterComplete = null,
        Func<OperationContext, Task>? afterFail = null,
        CancellationToken ct = default)
    {
        if (!_config.Types.ContainsKey(type.Name))
            throw new ArgumentException($"Notification type '{type.Name}' not registered. Use config.DefineType().");

        var registeredType = _config.Types[type.Name];
        var channels = registeredType.Channels
            .Where(name => _config.Channels.ContainsKey(name))
            .Select(name => _config.Channels[name])
            .ToList();

        if (channels.Count == 0)
            throw new InvalidOperationException(
                $"No channels for '{type.Name}'. Defined: [{string.Join(", ", registeredType.Channels)}]. Registered: [{string.Join(", ", _config.Channels.Keys)}]");

        var op = NotifyOps.SendMultiChannel(
            recipient,
            titleOverride ?? registeredType.Title ?? type.Name,
            messageOverride ?? registeredType.Message ?? type.Name,
            channels,
            type: registeredType,
            priority: registeredType.Priority,
            extraData: data);

        if (afterComplete != null) op.Hooks.AfterComplete = afterComplete;
        if (afterFail != null) op.Hooks.AfterFail = afterFail;

        return await _engine.ExecuteAsync(op, ct);
    }

    /// <summary>
    /// إرسال مباشر بدون نوع مُسبق (لحالات خاصة).
    /// </summary>
    public async Task<OperationResult> SendDirectAsync(
        PartyId recipient,
        string title,
        string message,
        INotificationChannel[] channels,
        NotificationPriority? priority = null,
        object? data = null,
        CancellationToken ct = default)
    {
        var op = NotifyOps.SendMultiChannel(recipient, title, message, channels,
            priority: priority, extraData: data);
        return await _engine.ExecuteAsync(op, ct);
    }

    public async Task<OperationResult> MarkReadAsync(
        PartyId user, Guid? originalOpId = null, CancellationToken ct = default)
    {
        return await _engine.ExecuteAsync(NotifyOps.MarkRead(user, originalOpId), ct);
    }
}

public static class NotificationExtensions
{
    public static IServiceCollection AddNotifications(this IServiceCollection services, Action<NotificationConfig> configure)
    {
        var config = new NotificationConfig();
        configure(config);
        services.AddSingleton(config);
        services.AddScoped<Notifier>();
        return services;
    }
}
