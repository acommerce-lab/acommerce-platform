namespace ACommerce.Client.Operations;

/// <summary>
/// وضع تشغيل القالب.
///
/// Callback: القالب يُطلق أحداثاً (EventCallback) والصفحة تنفّذ العملية يدوياً.
///           هذا الوضع القديم — يُبقى للتوافق العكسي.
///
/// Operation: القالب يحمل ITemplateEngine مباشرة وينفّذ العملية داخلياً.
///            الصفحة تُمرر فقط: Engine + Store + دوال بناء العمليات (Func&lt;...&gt;).
///            هذا الوضع الجديد — يُقلّص الصفحة من 80 سطراً إلى ~12.
/// </summary>
public enum TemplateOperationMode
{
    /// <summary>الوضع الكلاسيكي — القالب يُطلق أحداثاً والصفحة تتحكم.</summary>
    Callback = 0,

    /// <summary>الوضع الحديث — القالب يُنفّذ العمليات مباشرة عبر ITemplateEngine.</summary>
    Operation = 1
}
