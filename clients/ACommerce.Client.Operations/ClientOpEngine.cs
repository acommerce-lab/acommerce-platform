using ACommerce.Client.Operations.Interceptors;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;
using Microsoft.Extensions.Logging;

namespace ACommerce.Client.Operations;

/// <summary>
/// محرّك العمليات في العميل.
///
/// يختلف عن OpEngine في الخادم بأنه:
///   - لا ينفّذ المحللات الثقيلة (تلك مسؤولية الخادم)
///   - يستبدل خطوة Execute بإرسال الـ Operation عبر IOperationDispatcher
///   - يدمج OperationDescriptor من ردّ الخادم مع الـ Operation المحلية
///   - يُطبّق StateBridge تلقائياً عبر IStateApplier المحقون (إن وُجد)
/// </summary>
public class ClientOpEngine : ITemplateEngine
{
    private readonly IOperationDispatcher _dispatcher;
    private readonly ILogger<ClientOpEngine> _logger;
    private readonly IStateApplier? _stateApplier;

    public event Action<Operation, OperationEnvelope<object>>? OnOperationCompleted;
    public event Action<Operation, Exception>? OnOperationFailed;

    public ClientOpEngine(
        IOperationDispatcher dispatcher,
        ILogger<ClientOpEngine> logger,
        IStateApplier? stateApplier = null)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _stateApplier = stateApplier;
    }

    /// <summary>
    /// تنفيذ عملية كاملة:
    /// 1) محللات ما قبل النواة المحلية (تحقق سريع قبل HTTP)
    /// 2) إرسال للخادم
    /// 3) دمج رد الخادم
    /// 4) استدعاء أحداث الإكمال
    /// </summary>
    public async Task<OperationEnvelope<T>> ExecuteAsync<T>(
        Operation localOp,
        object? payload = null,
        CancellationToken ct = default)
    {
        try
        {
            // 1) محللات ما قبل النواة المحلية (مثل: تحقق من وجود توكن، حقول مطلوبة...)
            foreach (var preAnalyzer in localOp.PreAnalyzers)
            {
                var result = await preAnalyzer.AnalyzeAsync(new OperationContext(localOp, null!, ct));
                if (!result.Passed && result.IsBlocking)
                {
                    _logger.LogWarning("[ClientOpEngine] PreAnalyzer {Name} failed: {Msg}",
                        preAnalyzer.Name, result.Message);

                    return new OperationEnvelope<T>
                    {
                        Operation = OperationEnvelopeFactory.ToDescriptor(localOp,
                            new OperationResult
                            {
                                OperationId = localOp.Id,
                                OperationType = localOp.Type,
                                Success = false,
                                FailedAnalyzer = preAnalyzer.Name,
                                ErrorMessage = result.Message
                            }),
                        Error = new OperationError
                        {
                            Code = "client_precheck_failed",
                            Message = result.Message
                        }
                    };
                }
            }

            // 2) إرسال للخادم
            var envelope = await _dispatcher.DispatchAsync<T>(localOp, payload, ct);

            // 3) دمج رد الخادم في القيد المحلي
            OperationMerger.Merge(localOp, envelope.Operation);

            // 4) تطبيق جسر الحالة تلقائياً (إن كان المحقون متاحاً)
            var asObject = new OperationEnvelope<object>
            {
                Data = envelope.Data,
                Operation = envelope.Operation,
                Error = envelope.Error,
                Meta = envelope.Meta
            };
            if (_stateApplier != null)
                await _stateApplier.ApplyAsync(asObject, ct);

            // 5) إطلاق أحداث الإكمال
            OnOperationCompleted?.Invoke(localOp, asObject);

            return envelope;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClientOpEngine] Operation {Type} failed", localOp.Type);
            OnOperationFailed?.Invoke(localOp, ex);
            throw;
        }
    }
}
