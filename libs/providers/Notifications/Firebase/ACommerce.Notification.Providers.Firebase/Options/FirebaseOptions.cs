namespace ACommerce.Notification.Providers.Firebase.Options;

/// <summary>
/// إعدادات Firebase Cloud Messaging.
/// </summary>
public class FirebaseOptions
{
    public const string SectionName = "Notifications:Firebase";

    /// <summary>
    /// مسار ملف Service Account JSON من Google Cloud Console.
    /// </summary>
    public string? CredentialsFilePath { get; set; }

    /// <summary>
    /// محتوى Service Account JSON مباشرة (بديل للملف).
    /// مفيد عند التحميل من Vault أو متغيرات البيئة.
    /// </summary>
    public string? CredentialsJson { get; set; }

    /// <summary>معرف مشروع Firebase (Project ID)</summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// إزالة الرموز غير الصالحة تلقائياً عند فشل الإرسال (NotRegistered, InvalidRegistration).
    /// </summary>
    public bool RemoveInvalidTokens { get; set; } = true;

    /// <summary>
    /// إعدادات افتراضية للإشعار - يمكن تجاوزها لكل رسالة.
    /// </summary>
    public string? DefaultSound { get; set; } = "default";
    public string? DefaultIcon { get; set; }
    public string? DefaultColor { get; set; }
    public string? DefaultClickAction { get; set; }
}
