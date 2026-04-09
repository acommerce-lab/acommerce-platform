using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.Client.Operations;

/// <summary>
/// تجريد إرسال العملية للخادم. تنفيذه يكون في طبقة HTTP المحاسبية.
/// </summary>
public interface IOperationDispatcher
{
    /// <summary>
    /// إرسال عملية محلية وانتظار مغلف الخادم.
    /// </summary>
    Task<OperationEnvelope<T>> DispatchAsync<T>(
        Operation localOp,
        object? payload = null,
        CancellationToken ct = default);
}
