namespace ACommerce.OperationEngine.Core;

/// <summary>
/// محلل عمليات: يراقب علامات محددة ويفرض قواعد.
///
/// المحلل لا يعرف "ما هي" العملية - يعرف فقط "ما العلامات" التي يهتم بها.
/// هذا يجعله قابلاً للتركيب: أضف محللاً لأي عملية بغض النظر عن نوعها.
///
/// أمثلة:
///   BalanceAnalyzer يراقب [direction:*] ويتحقق أن sum(debit) == sum(credit)
///   SequenceAnalyzer يراقب [workflow:*] ويتحقق من الترتيب
///   DeliveryAnalyzer يراقب [channel:*] ويتحقق من التسليم
/// </summary>
public interface IOperationAnalyzer
{
    /// <summary>
    /// اسم المحلل (للتسجيل والتقارير)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// ما العلامات التي يراقبها هذا المحلل؟
    /// المحرك يستدعيه فقط إذا العملية تحتوي علامة من هذه القائمة.
    /// قائمة فارغة = يعمل دائماً.
    /// </summary>
    IReadOnlyList<string> WatchedTagKeys { get; }

    /// <summary>
    /// التحليل: يفحص العملية ويُرجع النتيجة.
    /// يمكنه الفحص + إنتاج تقارير + إطلاق أحداث.
    /// </summary>
    Task<AnalyzerResult> AnalyzeAsync(OperationContext context);
}

/// <summary>
/// نتيجة التحليل
/// </summary>
public class AnalyzerResult
{
    /// <summary>
    /// هل نجح التحليل؟
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// رسالة (خطأ أو معلومة)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// هل يمنع التنفيذ عند الفشل؟ (بعض المحللات تحذيرية فقط)
    /// </summary>
    public bool IsBlocking { get; set; } = true;

    /// <summary>
    /// بيانات التقرير (مثل: الرصيد، عدد غير المكتمل، etc.)
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// أحداث يطلقها المحلل (يستلمها من يراقب)
    /// </summary>
    public List<AnalyzerEvent> Events { get; set; } = new();

    public static AnalyzerResult Pass(string? message = null) => new() { Passed = true, Message = message };
    public static AnalyzerResult Fail(string message, bool blocking = true) => new() { Passed = false, Message = message, IsBlocking = blocking };
    public static AnalyzerResult Warning(string message) => new() { Passed = true, Message = message, IsBlocking = false };
}

/// <summary>
/// حدث يُطلقه المحلل
/// </summary>
public class AnalyzerEvent
{
    public string Name { get; set; } = default!;
    public string AnalyzerName { get; set; } = default!;
    public Dictionary<string, object> Data { get; set; } = new();
}
