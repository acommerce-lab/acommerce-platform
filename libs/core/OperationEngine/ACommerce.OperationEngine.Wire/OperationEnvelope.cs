namespace ACommerce.OperationEngine.Wire;

/// <summary>
/// المغلف القياسي لكل ردّ HTTP من خدمة محاسبية.
/// يحمل البيانات + بنية العملية الكاملة + خطأ اختياري.
/// </summary>
public class OperationEnvelope<T>
{
    /// <summary>الحمولة الفعلية (الكيان أو نتيجة العملية)</summary>
    public T? Data { get; set; }

    /// <summary>وصف العملية المحاسبية الكامل</summary>
    public OperationDescriptor Operation { get; set; } = new();

    /// <summary>خطأ مُهيكل في حالة فشل (مكمّل لـ Operation.Status)</summary>
    public OperationError? Error { get; set; }

    /// <summary>بيانات وصفية إضافية</summary>
    public Dictionary<string, object>? Meta { get; set; }
}

/// <summary>
/// نسخة مغلف غير معرّفة بنوع - للاستخدام عندما لا توجد بيانات.
/// </summary>
public class OperationEnvelope : OperationEnvelope<object>
{
}

/// <summary>
/// وصف عملية محاسبية - serializable.
/// </summary>
public class OperationDescriptor
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public string? Description { get; set; }
    public string Status { get; set; } = "Unknown"; // Success | Failed | Partial
    public DateTime ExecutedAt { get; set; }
    public string? ParentOperationId { get; set; }

    public List<PartyDescriptor> Parties { get; set; } = new();
    public Dictionary<string, string> Tags { get; set; } = new();
    public List<AnalyzerOutcome> Analyzers { get; set; } = new();

    /// <summary>المحلل الذي فشل (إن وُجد)</summary>
    public string? FailedAnalyzer { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>عمليات فرعية (للقيود المُركّبة)</summary>
    public List<OperationDescriptor> SubOperations { get; set; } = new();
}

/// <summary>
/// طرف في العملية - serializable.
/// </summary>
public class PartyDescriptor
{
    public string Identity { get; set; } = default!;
    public decimal Value { get; set; }
    public string? Role { get; set; }
    public string? Direction { get; set; } // debit | credit
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// نتيجة محلل واحد.
/// </summary>
public class AnalyzerOutcome
{
    public string Name { get; set; } = default!;
    public string Phase { get; set; } = "pre"; // pre | post
    public bool Passed { get; set; }
    public bool IsBlocking { get; set; } = true;
    public string? Message { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// خطأ مُهيكل.
/// </summary>
public class OperationError
{
    public string Code { get; set; } = default!;
    public string? Message { get; set; }
    public string? Hint { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}
