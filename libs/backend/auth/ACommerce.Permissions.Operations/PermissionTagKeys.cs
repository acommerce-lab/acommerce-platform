using ACommerce.OperationEngine.Core;

namespace ACommerce.Permissions.Operations;

/// <summary>
/// مفاتيح العلامات القياسية لمعترض الصلاحيات.
/// </summary>
public static class PermissionTagKeys
{
    /// <summary>اسم الإذن المطلوب (مثل: "listings.delete")</summary>
    public static readonly TagKey Check = new("permission_check");

    /// <summary>اسم الدور المطلوب (مثل: "admin")</summary>
    public static readonly TagKey RoleCheck = new("role_check");

    /// <summary>معرّف المستخدم للفحص</summary>
    public static readonly TagKey UserId = new("permission_user_id");
}
