using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.OperationEngine.Core;

/// <summary>
/// محرك العمليات الموسومة (Layer 0).
///
/// لا يعرف ما هو "قيد محاسبي" أو "إشعار" أو "تدفق".
/// يعرف فقط: أطراف + علامات + محللات + أحداث.
///
/// الترتيب:
///   BeforeAnalyze → PreAnalyzers → AfterAnalyze
///   → BeforeValidate → Validate → AfterValidate
///   → BeforeExecute → Execute → AfterExecute
///   → BeforeSubOperations → SubOperations → AfterSubOperations
///   → BeforePostAnalyze → PostAnalyzers → AfterPostAnalyze
///   → BeforeComplete/BeforeFail → Result → AfterComplete/AfterFail
/// </summary>
public class OpEngine
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OpEngine> _logger;

    public OpEngine(IServiceProvider services, ILogger<OpEngine> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(Operation op, CancellationToken ct = default)
    {
        var ctx = new OperationContext(op, _services, ct);
        var result = new OperationResult { OperationId = op.Id, OperationType = op.Type, Context = ctx };
        var h = op.Hooks;

        _logger.LogInformation("[{Type}] {Id}: Start", op.Type, op.Id);

        // المعترضات المحقونة من DI registry (إن وُجد) - معترضات قبل التنفيذ وبعده
        var interceptorSource = _services.GetService<IInterceptorSource>();
        var preInterceptors = interceptorSource?.ResolveAnalyzers(op, "pre").ToList() ?? new();
        var postInterceptors = interceptorSource?.ResolveAnalyzers(op, "post").ToList() ?? new();

        try
        {
            // === 1. Pre-Analyzers (المعترضات أولاً ثم محللات القيد المحلية) ===
            op.Status = OperationStatus.Analyzing;
            await h.InvokeAsync(h.BeforeAnalyze, ctx);

            foreach (var analyzer in preInterceptors.Concat(op.PreAnalyzers))
            {
                // فقط إذا العملية تحتوي علامات يراقبها
                if (ShouldRun(analyzer, op))
                {
                    var ar = await analyzer.AnalyzeAsync(ctx);
                    ctx.AnalyzerResults.Add((analyzer.Name, ar));
                    ctx.AnalyzerEvents.AddRange(ar.Events);

                    if (!ar.Passed && ar.IsBlocking)
                    {
                        result.FailedAnalyzer = analyzer.Name;
                        return await FailOp(op, ctx, result, h, ar.Message ?? $"Analyzer {analyzer.Name} failed");
                    }
                }
            }

            await h.InvokeAsync(h.AfterAnalyze, ctx);

            // === 2. Validate ===
            await h.InvokeAsync(h.BeforeValidate, ctx);

            if (op.ValidateFunc != null && !await op.ValidateFunc(ctx))
            {
                result.ValidationErrors = ctx.GetValidationErrors();
                return await FailOp(op, ctx, result, h, result.ValidationErrors.FirstOrDefault() ?? "Validation failed");
            }

            op.Status = OperationStatus.Validated;
            await h.InvokeAsync(h.AfterValidate, ctx);

            // === 3. Execute ===
            op.Status = OperationStatus.Executing;

            // التحقق من عقود المزودين المطلوبة قبل التنفيذ
            foreach (var contractType in op.RequiredContracts)
            {
                if (_services.GetService(contractType) == null)
                    return await FailOp(op, ctx, result, h,
                        $"Required provider contract '{contractType.Name}' is not registered in DI.");
            }

            await h.InvokeAsync(h.BeforeExecute, ctx);

            if (op.ExecuteFunc != null)
                await op.ExecuteFunc(ctx);

            foreach (var party in op.Parties)
                if (party.Status == PartyStatus.Pending)
                    party.Status = PartyStatus.Completed;

            await h.InvokeAsync(h.AfterExecute, ctx);

            // === 4. SubOperations ===
            await h.InvokeAsync(h.BeforeSubOperations, ctx);

            foreach (var sub in op.SubOperations)
            {
                var subCtx = new OperationContext(sub, _services, ct) { ParentOperation = op };
                foreach (var item in ctx.Items) subCtx.Items.TryAdd(item.Key, item.Value);
                var subResult = await ExecuteAsync(sub, ct);
                result.SubResults.Add(subResult);
            }

            await h.InvokeAsync(h.AfterSubOperations, ctx);

            // === 5. Post-Analyzers (محللات القيد المحلية ثم المعترضات بعد التنفيذ) ===
            await h.InvokeAsync(h.BeforePostAnalyze, ctx);

            foreach (var analyzer in op.PostAnalyzers.Concat(postInterceptors))
            {
                if (ShouldRun(analyzer, op))
                {
                    var ar = await analyzer.AnalyzeAsync(ctx);
                    ctx.AnalyzerResults.Add((analyzer.Name, ar));
                    ctx.AnalyzerEvents.AddRange(ar.Events);
                }
            }

            await h.InvokeAsync(h.AfterPostAnalyze, ctx);

            // === 6. PostValidate ===
            if (op.PostValidateFunc != null)
                await op.PostValidateFunc(ctx);

            // === 7. Determine status ===
            DetermineStatus(op, result);

            // === 8. Complete/Fail hooks ===
            if (result.Success)
            {
                await h.InvokeAsync(h.BeforeComplete, ctx);
                await h.InvokeAsync(h.AfterComplete, ctx);
            }
            else
            {
                await h.InvokeAsync(h.BeforeFail, ctx);
                await h.InvokeAsync(h.AfterFail, ctx);
            }

            _logger.LogInformation("[{Type}] {Id}: {Status}", op.Type, op.Id, op.Status);
        }
        catch (Exception ex)
        {
            op.Status = OperationStatus.Failed;
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "[{Type}] {Id}: Exception", op.Type, op.Id);

            await h.InvokeAsync(h.BeforeError, ctx, ex);
            await h.InvokeAsync(h.AfterError, ctx, ex);
        }

        return result;
    }

    private static bool ShouldRun(IOperationAnalyzer analyzer, Operation op)
    {
        if (analyzer.WatchedTagKeys.Count == 0) return true;
        return analyzer.WatchedTagKeys.Any(key =>
            op.HasTag(key) || op.Parties.Any(p => p.HasTag(key)));
    }

    private static void DetermineStatus(Operation op, OperationResult result)
    {
        var subs = result.SubResults;
        if (subs.Count == 0 || subs.All(r => r.Success))
        { op.Status = OperationStatus.Completed; op.CompletedAt = DateTime.UtcNow; result.Success = true; }
        else if (subs.Any(r => r.Success))
        { op.Status = OperationStatus.PartiallyCompleted; op.CompletedAt = DateTime.UtcNow; result.Success = true; result.IsPartial = true; }
        else
        { op.Status = OperationStatus.Failed; result.Success = false; result.ErrorMessage = "All sub-operations failed"; }
    }

    private static async Task<OperationResult> FailOp(Operation op, OperationContext ctx, OperationResult result,
        OperationLifecycleHooks h, string error)
    {
        op.Status = OperationStatus.Failed;
        result.Success = false;
        result.ErrorMessage = error;
        await h.InvokeAsync(h.BeforeFail, ctx);
        await h.InvokeAsync(h.AfterFail, ctx);
        return result;
    }
}

public class OperationResult
{
    public Guid OperationId { get; set; }
    public string OperationType { get; set; } = default!;
    public bool Success { get; set; }
    public bool IsPartial { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FailedAnalyzer { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<OperationResult> SubResults { get; set; } = new();
    public OperationContext? Context { get; internal set; }
}
