namespace ACommerce.SharedKernel.Abstractions.Entities;

/// <summary>
/// سجل قيد: يُحفظ لكل عملية تُنفَّذ بنجاح أو فشل.
/// يُستخدَم من JournalInterceptor (opt-in عبر AddOperationJournal).
///
/// يتيح:
///   - مسار تدقيق كامل بدون معترضات مخصصة لكل تطبيق
///   - استعلامات الحسابات (كل الأطراف حيث identity = X)
///   - إعادة التشغيل والتصحيح
/// </summary>
public class JournalEntry : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>نوع العملية (مثل: thing.create, auth.login)</summary>
    public string OperationType { get; set; } = default!;

    /// <summary>معرف العملية الأصلية</summary>
    public Guid OperationId { get; set; }

    /// <summary>حالة العملية النهائية (Completed, Failed, ...)</summary>
    public string Status { get; set; } = default!;

    /// <summary>هل نجحت العملية؟</summary>
    public bool Success { get; set; }

    /// <summary>رسالة الخطأ في حالة الفشل</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>تاريخ اكتمال العملية</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>معرف العملية الأب (للعمليات الفرعية)</summary>
    public Guid? ParentOperationId { get; set; }

    /// <summary>الأطراف مُسلسَلة JSON: [{Identity, Value, Tags:[{Key,Value}]}]</summary>
    public string PartiesJson { get; set; } = "[]";

    /// <summary>علامات العملية مُسلسَلة JSON: [{Key, Value}]</summary>
    public string TagsJson { get; set; } = "[]";
}
