using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.Client.Operations;

/// <summary>
/// محرّك العمليات الذي تحتاجه القوالب.
///
/// القوالب في ACommerce.Templates.* تستقبل ITemplateEngine بدلاً من
/// ClientOpEngine المحدد — هذا يُبقي القوالب معزولة عن تفاصيل DI.
///
/// ClientOpEngine يطبّق هذه الواجهة تلقائياً.
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// تنفيذ عملية وإرسالها للخادم.
    /// يُطبّق جسر الحالة تلقائياً بعد نجاح العملية.
    /// </summary>
    Task<OperationEnvelope<T>> ExecuteAsync<T>(
        Operation localOp,
        object? payload = null,
        CancellationToken ct = default);
}
