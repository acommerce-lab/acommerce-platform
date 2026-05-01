using ACommerce.OperationEngine.Core;

namespace ACommerce.OperationEngine.DataInterceptors;

/// <summary>
/// أنواع العمليات القياسية التي يدعمها معترض البيانات العام.
/// </summary>
public static class DataOperationTypes
{
    public static readonly OperationType Create   = new("data.create");
    public static readonly OperationType ReadAll  = new("data.read_all");
    public static readonly OperationType ReadById = new("data.read_by_id");
    public static readonly OperationType Update   = new("data.update");
    public static readonly OperationType Delete   = new("data.delete");
}

/// <summary>
/// مفاتيح وقيم العلامات الخاصة بعمليات البيانات.
/// </summary>
public static class OperationTags
{
    public static readonly TagKey DbAction     = new("db_action");
    public static readonly TagKey TargetEntity = new("target_entity");

    /// <summary>
    /// القيم النصية القديمة للتوافق مع الإصدارات السابقة.
    /// </summary>
    public static class DbActions
    {
        public const string Create = "create";
        public const string Update = "update";
        public const string Delete = "delete";
        public const string Read   = "read";
    }
}

/// <summary>
/// نوع كيان مُكتَّب — يطابق اسماً مسجَّلاً في
/// <c>EntityDiscoveryRegistry</c>. كلّ kit يُعلن instances ثابتة لكياناته،
/// والتطبيق يضع entity من نفس النوع عبر <c>ctx.WithEntity&lt;T&gt;()</c>.
///
/// <para>قاعدة عامّة: <c>EntityKind.Name</c> = اسم الكيان CLR (مع/بدون
/// لاحقة "Entity"). الـ <c>DataOperationHandler</c> يبحث في
/// <c>EntityDiscoveryRegistry</c> بهذا الاسم لإنشاء repository ديناميكيّاً.</para>
///
/// <para>الاستهلاك في kit:
/// <code>
/// public static class ChatEntityKinds
/// {
///     public static readonly EntityKind Message      = new("Message");
///     public static readonly EntityKind Conversation = new("Conversation");
/// }
/// </code></para>
/// </summary>
public sealed class EntityKind : IEquatable<EntityKind>
{
    public string Name { get; }

    public EntityKind(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("EntityKind.Name cannot be empty", nameof(name));
        Name = name;
    }

    public override string ToString() => Name;
    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as EntityKind);
    public bool Equals(EntityKind? other) => other is not null && Name == other.Name;

    public static implicit operator string(EntityKind k) => k.Name;
    public static bool operator ==(EntityKind? a, EntityKind? b) => Equals(a, b);
    public static bool operator !=(EntityKind? a, EntityKind? b) => !Equals(a, b);
}
