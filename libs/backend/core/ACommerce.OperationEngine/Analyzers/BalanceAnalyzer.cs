using ACommerce.OperationEngine.Core;

namespace ACommerce.OperationEngine.Analyzers;

/// <summary>
/// محلل التوازن المحاسبي.
/// يراقب علامة [direction] على الأطراف.
/// يتحقق: مجموع قيم الأطراف بعلامة [direction:debit] = مجموع [direction:credit]
///
/// هذا هو "القيد المحاسبي" - ليس نوعاً خاصاً من العمليات،
/// بل محلل يُضاف لأي عملية تريد التوازن.
/// </summary>
public class BalanceAnalyzer : IOperationAnalyzer
{
    public string Name => "Balance";
    public IReadOnlyList<string> WatchedTagKeys { get; } = new[] { "direction" };

    public Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
    {
        var op = context.Operation;

        var debits = op.Parties.Where(p => p.HasTag("direction", "debit")).Sum(p => p.Value);
        var credits = op.Parties.Where(p => p.HasTag("direction", "credit")).Sum(p => p.Value);

        if (Math.Abs(debits - credits) < 0.001m)
        {
            var result = AnalyzerResult.Pass($"Balanced: {debits}");
            result.Data["debit_total"] = debits;
            result.Data["credit_total"] = credits;
            return Task.FromResult(result);
        }

        return Task.FromResult(AnalyzerResult.Fail(
            $"Imbalanced: debit={debits}, credit={credits}, diff={debits - credits}"));
    }
}

/// <summary>
/// محلل التسلسل.
/// يراقب علامة [workflow] ويتحقق أن الخطوة السابقة مكتملة.
/// </summary>
public class SequenceAnalyzer : IOperationAnalyzer
{
    public string Name => "Sequence";
    public IReadOnlyList<string> WatchedTagKeys { get; } = new[] { "workflow" };

    private readonly Func<string, string, Task<bool>>? _checkPreviousStep;

    /// <param name="checkPreviousStep">
    /// دالة يوفرها المطور: (currentStep, operationType) → هل الخطوة السابقة مكتملة؟
    /// </param>
    public SequenceAnalyzer(Func<string, string, Task<bool>>? checkPreviousStep = null)
    {
        _checkPreviousStep = checkPreviousStep;
    }

    public async Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
    {
        var step = context.Operation.GetTagValue("workflow");
        if (step == null) return AnalyzerResult.Pass("No workflow tag");

        if (_checkPreviousStep != null)
        {
            var ok = await _checkPreviousStep(step, context.Operation.Type);
            if (!ok) return AnalyzerResult.Fail($"Previous step not completed for workflow:{step}");
        }

        return AnalyzerResult.Pass($"Sequence OK: {step}");
    }
}

/// <summary>
/// محلل المعكوسات.
/// يتحقق أن العملية الأصلية موجودة ومكتملة.
/// </summary>
public class FulfillmentAnalyzer : IOperationAnalyzer
{
    public string Name => "Fulfillment";
    public IReadOnlyList<string> WatchedTagKeys { get; } = new[] { "relation" };

    private readonly Func<Guid, Task<bool>>? _checkOriginalExists;

    public FulfillmentAnalyzer(Func<Guid, Task<bool>>? checkOriginalExists = null)
    {
        _checkOriginalExists = checkOriginalExists;
    }

    public async Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
    {
        var op = context.Operation;
        if (op.OriginalOperationId == null || op.Relation == null)
            return AnalyzerResult.Pass("No relation defined");

        if (_checkOriginalExists != null)
        {
            var exists = await _checkOriginalExists(op.OriginalOperationId.Value);
            if (!exists)
                return AnalyzerResult.Fail($"Original operation {op.OriginalOperationId} not found");
        }

        var result = AnalyzerResult.Pass($"{op.Relation} of {op.OriginalOperationId}");
        result.Data["relation"] = op.Relation.ToString()!;
        result.Data["original_id"] = op.OriginalOperationId.Value;
        return result;
    }
}
