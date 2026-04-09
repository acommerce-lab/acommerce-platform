using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.Client.Operations.Interceptors;

/// <summary>
/// معترض إرسال HTTP - معترض ما قبل التنفيذ يربط القيد بالخادم.
///
/// أي قيد على العميل يحوي علامة "client_dispatch=true" يُعالَج كالتالي:
///   1. يُرسل عبر IOperationDispatcher (طبقة HTTP)
///   2. يستلم OperationEnvelope من الخادم
///   3. يضع المغلف في ctx ليستهلكه ClientOpEngine
///
/// بهذا تكون طبقة HTTP نفسها معترضاً محاسبياً - يُحقن في أي قيد عميل
/// يحتاج إرسالاً للخادم. هذا يحقق التماثل: Server interceptors + Client interceptors.
/// </summary>
public class HttpDispatchInterceptor : IOperationInterceptor
{
    private readonly IOperationDispatcher _dispatcher;

    public string Name => "HttpDispatchInterceptor";
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public HttpDispatchInterceptor(IOperationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public bool AppliesTo(Operation op) => op.HasTag("client_dispatch", "true");

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var op = context.Operation;
        context.TryGet<object>("payload", out var payload);

        // إرسال للخادم - الـ envelope يصبح متاحاً في الـ context
        var envelope = await _dispatcher.DispatchAsync<object>(op, payload, context.CancellationToken);
        context.Set("server_envelope", envelope);

        // لو فشل الخادم، نُرجع فشلاً للقيد المحلي
        if (envelope.Operation.Status != "Success")
        {
            return new AnalyzerResult
            {
                Passed = false,
                IsBlocking = true,
                Message = envelope.Error?.Code ?? envelope.Operation.ErrorMessage ?? "server_error",
                Data = new Dictionary<string, object>
                {
                    ["server_status"] = envelope.Operation.Status,
                    ["server_error"] = envelope.Error?.Message ?? string.Empty
                }
            };
        }

        return AnalyzerResult.Pass();
    }
}
