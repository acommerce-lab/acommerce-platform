using ACommerce.Notification.Operations.Abstractions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.Realtime.Operations.Abstractions;

namespace ACommerce.Notification.Operations.Operations;

public static class NotifyOps
{
    public static Operation SendSingle(
        PartyId recipient,
        string title,
        string message,
        INotificationChannel channel,
        NotificationType? type = null,
        NotificationPriority? priority = null)
    {
        var p = priority ?? NotificationPriority.Normal;

        return Entry.Create("notify.send")
            .Describe($"Notify {recipient}: {title}")
            .From(PartyId.System, 1, (RT.Role, "sender"))
            .To(recipient, 1, (RT.Role, "recipient"), (RT.Delivery, DeliveryStatus.Pending))
            .Tag(NotifyTags.Channel, channel.ChannelName)
            .Tag(NotifyTags.NotificationType, type?.Name ?? "direct")
            .Tag(NotifyTags.Priority, p)
            .Validate(async ctx =>
            {
                var ok = await channel.ValidateAsync(recipient.Id, ctx.CancellationToken);
                if (!ok) ctx.AddValidationError("channel", $"{channel.ChannelName} unavailable for {recipient}");
                return ok;
            })
            .Execute(async ctx =>
            {
                var sent = await channel.SendAsync(recipient.Id, title, message, null, ctx.CancellationToken);
                var r = ctx.Operation.GetPartiesByTag(RT.Role, "recipient").FirstOrDefault();
                if (r != null) { r.RemoveTag(RT.Delivery); r.AddTag(RT.Delivery, sent ? DeliveryStatus.Sent : DeliveryStatus.Failed); }
                ctx.Set("sent", sent);
            })
            .Build();
    }

    public static Operation SendMultiChannel(
        PartyId recipient,
        string title,
        string message,
        IEnumerable<INotificationChannel> channels,
        NotificationType? type = null,
        NotificationPriority? priority = null,
        object? extraData = null)
    {
        var channelList = channels.ToList();
        var p = priority ?? NotificationPriority.Normal;

        var builder = Entry.Create("notify.multi")
            .Describe($"Multi-notify {recipient}: {title}")
            .From(PartyId.System, channelList.Count, (RT.Role, "sender"))
            .To(recipient, channelList.Count, (RT.Role, "recipient"))
            .Tag(NotifyTags.NotificationType, type?.Name ?? "direct")
            .Tag(NotifyTags.Priority, p);

        foreach (var ch in channelList)
            builder.Tag(NotifyTags.Channel, ch.ChannelName);

        builder.Execute(ctx =>
        {
            ctx.Set("title", title);
            ctx.Set("message", message);
            if (extraData != null) ctx.Set("extraData", extraData);
        });

        foreach (var channel in channelList)
        {
            var ch = channel;
            builder.WithSub($"notify.channel.{ch.ChannelName}", sub =>
            {
                sub.Party(PartyId.Channel(ch.ChannelName), 1,
                    ("direction", "debit"), (NotifyTags.ChannelDelivery, DeliveryStatus.Pending));
                sub.Party(recipient, 1, ("direction", "credit"), (RT.Role, "recipient"));
                sub.Execute(async ctx =>
                {
                    var sent = await ch.SendAsync(recipient.Id, title, message, extraData, ctx.CancellationToken);
                    var chParty = ctx.Operation.GetPartiesByTag("direction", "debit").FirstOrDefault();
                    if (chParty != null)
                    {
                        chParty.RemoveTag(NotifyTags.ChannelDelivery);
                        chParty.AddTag(NotifyTags.ChannelDelivery, sent ? DeliveryStatus.Sent : DeliveryStatus.Failed);
                    }
                });
            });
        }

        return builder.Build();
    }

    public static Operation MarkRead(PartyId user, Guid? originalOpId = null)
    {
        var builder = Entry.Create("notify.read")
            .From(user, 1, (RT.Delivery, DeliveryStatus.Read))
            .To(PartyId.System, 1)
            .Execute(ctx => { ctx.Set("readAt", DateTime.UtcNow); });

        if (originalOpId != null)
            builder.Fulfills(originalOpId.Value);

        return builder.Build();
    }

    public static Operation Subscribe(PartyId user, PartyId topic)
    {
        return Entry.Create("notify.subscribe")
            .From(user, 1)
            .To(topic, 1, (RT.Group, $"topic_{topic.Id}"))
            .Tag("topic", topic.Id)
            .Build();
    }
}
