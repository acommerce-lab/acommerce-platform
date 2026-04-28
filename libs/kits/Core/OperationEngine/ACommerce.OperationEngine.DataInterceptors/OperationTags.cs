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
