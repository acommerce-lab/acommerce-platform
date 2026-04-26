namespace ACommerce.Realtime.Operations.Abstractions;

/// <summary>
/// حالة التسليم - كائن بدل نص.
/// المبرمج لا يكتب "pending" بل DeliveryStatus.Pending
/// </summary>
public sealed class DeliveryStatus
{
    public string Value { get; }
    private DeliveryStatus(string value) => Value = value;

    public static readonly DeliveryStatus Pending = new("pending");
    public static readonly DeliveryStatus Sent = new("sent");
    public static readonly DeliveryStatus Delivered = new("delivered");
    public static readonly DeliveryStatus Read = new("read");
    public static readonly DeliveryStatus Failed = new("failed");

    /// <summary>
    /// للقيم المخصصة التي لم نتوقعها
    /// </summary>
    public static DeliveryStatus Custom(string value) => new(value);

    public override string ToString() => Value;
    public override bool Equals(object? obj) => obj is DeliveryStatus ds && ds.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();
    public static implicit operator string(DeliveryStatus ds) => ds.Value;
}

/// <summary>
/// حالة الحضور
/// </summary>
public sealed class PresenceStatus
{
    public string Value { get; }
    private PresenceStatus(string value) => Value = value;

    public static readonly PresenceStatus Online = new("online");
    public static readonly PresenceStatus Offline = new("offline");
    public static readonly PresenceStatus Away = new("away");
    public static readonly PresenceStatus Busy = new("busy");

    public static PresenceStatus Custom(string value) => new(value);
    public override string ToString() => Value;
    public static implicit operator string(PresenceStatus ps) => ps.Value;
}

/// <summary>
/// هوية الطرف - بدل نص "User:123" أو "System"
/// </summary>
public sealed class PartyId
{
    public string Type { get; }
    public string Id { get; }
    public string FullId { get; }

    private PartyId(string type, string id)
    {
        Type = type;
        Id = id;
        FullId = $"{type}:{id}";
    }

    public static PartyId User(string userId) => new("User", userId);
    public static PartyId System => new("System", "");
    public static PartyId Group(string groupId) => new("Group", groupId);
    public static PartyId Channel(string channelName) => new("Channel", channelName);
    public static PartyId Conversation(string convId) => new("Conversation", convId);
    public static PartyId Topic(string topic) => new("Topic", topic);
    public static PartyId All => new("All", "");
    public static PartyId Of(string type, string id) => new(type, id);

    public override string ToString() => string.IsNullOrEmpty(Id) ? Type : FullId;
    public static implicit operator string(PartyId pid) => pid.ToString();
}
