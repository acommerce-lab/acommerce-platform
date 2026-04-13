namespace ACommerce.OperationEngine.Journal;

/// <summary>
/// واجهة استعلامات الحسابات - تُعامل الأطراف كـ "حسابات" قابلة للاستعلام.
///
/// مبنية على جدول journal_entries (يُملأ من JournalInterceptor).
/// تحوّل الأطراف المُسجَّلة في القيود إلى سجلات قابلة للتجميع والتصفية.
///
/// الاستخدام النموذجي:
///   var parties = await query.GetPartiesAsync("User:abc123");
///   var balance = await query.GetBalanceAsync("User:abc123");
/// </summary>
public interface IAccountQuery
{
    /// <summary>
    /// جلب كل الأطراف التي طابقت identity في الدفتر.
    /// كل طرف يمثّل نصف قيد (debit أو credit) في عملية مُنفَّذة.
    /// </summary>
    Task<IReadOnlyList<JournalParty>> GetPartiesAsync(
        string identity,
        DateRange? dateRange = null,
        IEnumerable<(string Key, string Value)>? tags = null,
        CancellationToken ct = default);

    /// <summary>
    /// احتساب رصيد طرف: مجموع قيم أطرافه الناجحة.
    /// يمكن تخصيص دالة التجميع (الافتراضي: p => p.Value).
    /// </summary>
    Task<decimal> GetBalanceAsync(
        string identity,
        Func<JournalParty, decimal>? valueAggregator = null,
        DateRange? dateRange = null,
        CancellationToken ct = default);
}

/// <summary>
/// طرف واحد مُستخرَج من الدفتر - يجمع بيانات الطرف مع بيانات العملية.
/// </summary>
public record JournalParty(
    string Identity,
    decimal Value,
    string OperationType,
    Guid OperationId,
    DateTime Timestamp,
    bool Success,
    IReadOnlyList<(string Key, string Value)> Tags);

/// <summary>
/// نطاق زمني للاستعلام.
/// </summary>
public record DateRange(DateTime From, DateTime To);
