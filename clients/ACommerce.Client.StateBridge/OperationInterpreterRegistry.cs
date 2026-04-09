using ACommerce.OperationEngine.Wire;
using Microsoft.Extensions.Logging;

namespace ACommerce.Client.StateBridge;

/// <summary>
/// سجل المُفسّرات + موزّع الأحداث.
/// عند وصول OperationEnvelope من الخادم، ApplyAsync يمرّرها على كل
/// مُفسّر يدّعي CanInterpret = true ليطبّق تغييراته على الـ store.
///
/// هذا هو النقطة التي يلتقي فيها "القيد المحاسبي" مع "حالة التطبيق".
/// </summary>
public class OperationInterpreterRegistry<TStore>
{
    private readonly List<IOperationInterpreter<TStore>> _interpreters = new();
    private readonly ILogger<OperationInterpreterRegistry<TStore>>? _logger;

    /// <summary>حدث إخطاري - يُطلق بعد كل تفسير ناجح (مفيد لـ UI toasts)</summary>
    public event Action<OperationDescriptor, object?>? OnInterpreted;

    /// <summary>حدث فشل المُفسّر</summary>
    public event Action<OperationDescriptor, Exception>? OnInterpreterFailed;

    public OperationInterpreterRegistry(ILogger<OperationInterpreterRegistry<TStore>>? logger = null)
    {
        _logger = logger;
    }

    public OperationInterpreterRegistry<TStore> Add(IOperationInterpreter<TStore> interpreter)
    {
        _interpreters.Add(interpreter);
        return this;
    }

    /// <summary>
    /// تطبيق مغلف عملية على الـ store - يمرّ على كل المُفسّرات المطابقة.
    /// </summary>
    public async Task ApplyAsync<T>(OperationEnvelope<T> envelope, TStore store, CancellationToken ct = default)
    {
        // لا نفسّر العمليات الفاشلة تلقائياً - ربما طبقة أعلى تتعامل مع الأخطاء
        if (envelope.Operation.Status != "Success" && envelope.Error != null)
        {
            _logger?.LogDebug("[StateBridge] Skipping failed operation {Type}: {Code}",
                envelope.Operation.Type, envelope.Error.Code);
            return;
        }

        foreach (var interpreter in _interpreters)
        {
            if (!interpreter.CanInterpret(envelope.Operation)) continue;

            try
            {
                await interpreter.InterpretAsync(envelope.Operation, envelope.Data, store, ct);
                _logger?.LogDebug("[StateBridge] Applied {Interpreter} for {OpType}",
                    interpreter.GetType().Name, envelope.Operation.Type);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[StateBridge] {Interpreter} failed for {OpType}",
                    interpreter.GetType().Name, envelope.Operation.Type);
                OnInterpreterFailed?.Invoke(envelope.Operation, ex);
            }
        }

        OnInterpreted?.Invoke(envelope.Operation, envelope.Data);
    }
}
