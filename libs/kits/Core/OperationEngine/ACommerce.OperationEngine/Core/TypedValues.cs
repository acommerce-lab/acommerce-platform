namespace ACommerce.OperationEngine.Core;

/// <summary>
/// نوع عملية مُسمّى - value object. المطوّر يُعرّف ثوابت ثابتة أو يُحمّل
/// أنواعاً من قاعدة البيانات.
///
/// مثال:
///   public static class AshareOps {
///       public static readonly OperationType ListingCreate = new("listing.create");
///       public static readonly OperationType BookingCreate = new("booking.create");
///   }
///
///   Entry.Create(AshareOps.ListingCreate)...
///
/// يُحوَّل ضمنياً لـ string عند الحاجة، فيعمل مع أي API قديم يستقبل نصوصاً.
/// </summary>
public sealed class OperationType : IEquatable<OperationType>
{
    public string Name { get; }

    public OperationType(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("OperationType name cannot be empty", nameof(name));
        Name = name;
    }

    public override string ToString() => Name;
    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as OperationType);
    public bool Equals(OperationType? other) => other != null && Name == other.Name;

    public static implicit operator string(OperationType type) => type.Name;
    public static bool operator ==(OperationType? a, OperationType? b) => Equals(a, b);
    public static bool operator !=(OperationType? a, OperationType? b) => !Equals(a, b);
}

/// <summary>
/// مفتاح علامة مُسمّى - value object. يسمح بكتابة علامات آمنة النوع.
///
/// مثال:
///   public static class QuotaTags {
///       public static readonly TagKey Check = new("quota_check");
///       public static readonly TagKey UserId = new("quota_user_id");
///       public static readonly TagKey ScopeKey = new("quota_scope_key");
///       public static readonly TagKey ScopeValue = new("quota_scope_value");
///   }
/// </summary>
public sealed class TagKey : IEquatable<TagKey>
{
    public string Name { get; }

    public TagKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("TagKey name cannot be empty", nameof(name));
        Name = name;
    }

    public override string ToString() => Name;
    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as TagKey);
    public bool Equals(TagKey? other) => other != null && Name == other.Name;

    public static implicit operator string(TagKey key) => key.Name;
    public static bool operator ==(TagKey? a, TagKey? b) => Equals(a, b);
    public static bool operator !=(TagKey? a, TagKey? b) => !Equals(a, b);
}

/// <summary>
/// قيمة علامة مُسمّاة - value object. للقيم التي تأتي من كتالوج ثابت أو من DB.
///
/// مثال:
///   public static class QuotaValues {
///       public static readonly TagValue ListingsCreate = new("listings.create");
///       public static readonly TagValue MessagesSend = new("messages.send");
///   }
/// </summary>
public sealed class TagValue : IEquatable<TagValue>
{
    public string Value { get; }

    public TagValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("TagValue cannot be empty", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as TagValue);
    public bool Equals(TagValue? other) => other != null && Value == other.Value;

    public static implicit operator string(TagValue val) => val.Value;
    public static bool operator ==(TagValue? a, TagValue? b) => Equals(a, b);
    public static bool operator !=(TagValue? a, TagValue? b) => !Equals(a, b);
}

/// <summary>
/// دور طرف مُسمّى - value object. مفيد لثبات الأدوار (owner, customer, issuer, ...)
/// </summary>
public sealed class PartyRole : IEquatable<PartyRole>
{
    public string Name { get; }

    public PartyRole(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("PartyRole name cannot be empty", nameof(name));
        Name = name;
    }

    public override string ToString() => Name;
    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as PartyRole);
    public bool Equals(PartyRole? other) => other != null && Name == other.Name;

    public static implicit operator string(PartyRole role) => role.Name;
    public static bool operator ==(PartyRole? a, PartyRole? b) => Equals(a, b);
    public static bool operator !=(PartyRole? a, PartyRole? b) => !Equals(a, b);
}

/// <summary>
/// زوج (مفتاح، قيمة) ثابت — يحمل tag كاملاً معلَّباً. مفيد للـ markers
/// التي يتشارك بها kit وcompositions: تَعريف واحد محصور في مكان واحد بدل
/// نشر سلسلتَين منفصلتَين.
///
/// مثال:
///   public static class SupportMarkers {
///       public static readonly Marker IsTicketReply = new("kind", "support");
///   }
///
///   Entry.Create(MessageOps.Send)
///        .Mark(SupportMarkers.IsTicketReply);
/// </summary>
public sealed class Marker : IEquatable<Marker>
{
    public string Key { get; }
    public string Value { get; }

    public Marker(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))   throw new ArgumentException("Marker.Key cannot be empty", nameof(key));
        if (value is null)                     throw new ArgumentNullException(nameof(value));
        Key = key; Value = value;
    }

    public Marker(TagKey key, string value)         : this(key.Name, value) { }
    public Marker(TagKey key, TagValue value)       : this(key.Name, value.Value) { }
    public Marker(string key, TagValue value)       : this(key, value.Value) { }

    public override string ToString() => $"{Key}={Value}";
    public override int GetHashCode() => HashCode.Combine(Key, Value);
    public override bool Equals(object? obj) => Equals(obj as Marker);
    public bool Equals(Marker? other) => other is not null && Key == other.Key && Value == other.Value;

    public static bool operator ==(Marker? a, Marker? b) => Equals(a, b);
    public static bool operator !=(Marker? a, Marker? b) => !Equals(a, b);
}

/// <summary>
/// كنية طرف (party prefix) — يفصل نوع الكيان عن الـ id. مثال: "User",
/// "Agent", "Service". يُستعمل لتركيب <see cref="PartyRef"/>.
/// </summary>
public sealed class PartyKind : IEquatable<PartyKind>
{
    public string Value { get; }

    public PartyKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("PartyKind value cannot be empty", nameof(value));
        Value = value;
    }

    public static readonly PartyKind User    = new("User");
    public static readonly PartyKind Agent   = new("Agent");
    public static readonly PartyKind System  = new("System");
    public static readonly PartyKind Service = new("Service");
    public static readonly PartyKind Listing = new("Listing");
    public static readonly PartyKind Ticket  = new("Ticket");
    public static readonly PartyKind Conversation = new("Conversation");
    public static readonly PartyKind Report  = new("Report");

    public override string ToString() => Value;
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as PartyKind);
    public bool Equals(PartyKind? other) => other is not null && Value == other.Value;

    public static implicit operator string(PartyKind k) => k.Value;
    public static bool operator ==(PartyKind? a, PartyKind? b) => Equals(a, b);
    public static bool operator !=(PartyKind? a, PartyKind? b) => !Equals(a, b);
}

/// <summary>
/// مرجع طرف كامل: نوع + معرّف. ينتج "Kind:Id" stringified — متطابق مع
/// المنتظَر في operations الحاليّة فيُستعمل كبديل آمن نوعاً.
/// </summary>
public sealed class PartyRef : IEquatable<PartyRef>
{
    public PartyKind Kind { get; }
    public string Id { get; }

    public PartyRef(PartyKind kind, string id)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        if (id is null) throw new ArgumentNullException(nameof(id));
        Id = id;
    }

    public override string ToString() => $"{Kind.Value}:{Id}";
    public override int GetHashCode() => HashCode.Combine(Kind, Id);
    public override bool Equals(object? obj) => Equals(obj as PartyRef);
    public bool Equals(PartyRef? other) => other is not null && Kind == other.Kind && Id == other.Id;

    public static implicit operator string(PartyRef r) => r.ToString();
    public static bool operator ==(PartyRef? a, PartyRef? b) => Equals(a, b);
    public static bool operator !=(PartyRef? a, PartyRef? b) => !Equals(a, b);
}
