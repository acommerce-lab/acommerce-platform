namespace ACommerce.Culture.Abstractions;

/// <summary>
/// يحوّل بين DateTime UTC (مُخزَّنة في القاعدة) وDateTime محلية لمنطقة زمنية.
/// الاستخدام:
///   • معترض قبل الحفظ: `ToUtc(localTime, userTz)` لضمان أن كل التواريخ في القاعدة UTC.
///   • في التقديم: `ToLocal(utcTime, userTz)` لعرض وقت الرسالة بحسب منطقة قارئ الرسالة.
/// هذا يحلّ مشكلة دردشتين في منطقتين زمنيتين مختلفتين.
/// </summary>
public interface IDateTimeNormalizer
{
    /// <summary>يفترض أن الإدخال بالمنطقة الزمنية المعطاة، ويحوّل إلى UTC.</summary>
    DateTime ToUtc(DateTime local, TimeZoneInfo tz);

    /// <summary>يفترض أن الإدخال UTC، ويحوّل إلى المنطقة الزمنية المعطاة.</summary>
    DateTime ToLocal(DateTime utc, TimeZoneInfo tz);

    /// <summary>DateTimeOffset → UTC DateTime (يجرّد الإزاحة).</summary>
    DateTime FromOffsetToUtc(DateTimeOffset dto);
}
