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
