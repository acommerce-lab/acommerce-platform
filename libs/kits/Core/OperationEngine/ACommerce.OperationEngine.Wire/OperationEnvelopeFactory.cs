using ACommerce.OperationEngine.Core;

namespace ACommerce.OperationEngine.Wire;

/// <summary>
/// مصنع تحويل Operation/OperationResult/Party إلى Wire DTOs.
/// يُستخدم في الخادم لتغليف ردود المتحكمات، وفي العميل لإعادة بناء العمليات من الردود.
/// </summary>
public static class OperationEnvelopeFactory
{
    /// <summary>
    /// إنشاء مغلف ناجح من نتيجة عملية + بيانات.
    /// </summary>
    public static OperationEnvelope<T> From<T>(Operation operation, OperationResult result, T? data)
    {
        var envelope = new OperationEnvelope<T>
        {
            Data = data,
            Operation = ToDescriptor(operation, result)
        };

        if (!result.Success)
        {
            envelope.Error = new OperationError
            {
                Code = result.FailedAnalyzer ?? "operation_failed",
                Message = result.ErrorMessage,
                Details = new Dictionary<string, object>
                {
                    ["validationErrors"] = result.ValidationErrors
                }
            };
        }

        return envelope;
    }

    /// <summary>
    /// مغلف خطأ بدون عملية (للأخطاء قبل بناء القيد).
    /// </summary>
    public static OperationEnvelope<T> Error<T>(string code, string? message = null, string? hint = null)
    {
        return new OperationEnvelope<T>
        {
            Data = default,
            Operation = new OperationDescriptor
            {
                Id = Guid.Empty,
                Type = "unknown",
                Status = "Failed",
                ExecutedAt = DateTime.UtcNow
            },
            Error = new OperationError { Code = code, Message = message, Hint = hint }
        };
    }

    /// <summary>
    /// مغلف معلوماتي (Read-only) - يحمل بيانات بدون عملية محاسبية حقيقية.
    /// مفيد لـ GET endpoints.
    /// </summary>
    public static OperationEnvelope<T> Info<T>(string opType, T data)
    {
        return new OperationEnvelope<T>
        {
            Data = data,
            Operation = new OperationDescriptor
            {
                Id = Guid.NewGuid(),
                Type = opType,
                Status = "Success",
                ExecutedAt = DateTime.UtcNow
            }
        };
    }

    /// <summary>
    /// تحويل Operation + OperationResult إلى OperationDescriptor.
    /// </summary>
    public static OperationDescriptor ToDescriptor(Operation operation, OperationResult result)
    {
        var descriptor = new OperationDescriptor
        {
            Id = operation.Id,
            Type = operation.Type,
            Description = operation.Description,
            Status = result.Success ? "Success" : (result.IsPartial ? "Partial" : "Failed"),
            ExecutedAt = DateTime.UtcNow,
            FailedAnalyzer = result.FailedAnalyzer,
            ErrorMessage = result.ErrorMessage,
            Tags = operation.Tags.GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.First().Value),
            Parties = operation.Parties.Select(ToPartyDescriptor).ToList(),
            Analyzers = ExtractAnalyzerOutcomes(operation, result)
        };

        return descriptor;
    }

    private static PartyDescriptor ToPartyDescriptor(Party party)
    {
        var tags = party.Tags.GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.First().Value);

        return new PartyDescriptor
        {
            Identity = party.Identity,
            Value = party.Value,
            Direction = tags.GetValueOrDefault("direction"),
            Role = tags.GetValueOrDefault("role"),
            Tags = tags
        };
    }

    /// <summary>
    /// نخرج المحللات من أسماء PreAnalyzers + PostAnalyzers بدون نتائجها التفصيلية
    /// (المحلل لا يخزن نتيجته في الـ Operation - فقط في الـ Result).
    /// لو احتجنا تتبع كامل لاحقاً نضيف Analyzer trace في Operation.
    /// </summary>
    private static List<AnalyzerOutcome> ExtractAnalyzerOutcomes(Operation operation, OperationResult result)
    {
        var outcomes = new List<AnalyzerOutcome>();

        foreach (var pre in operation.PreAnalyzers)
        {
            outcomes.Add(new AnalyzerOutcome
            {
                Name = pre.Name,
                Phase = "pre",
                Passed = result.Success || result.FailedAnalyzer != pre.Name,
                Message = result.FailedAnalyzer == pre.Name ? result.ErrorMessage : null
            });
        }

        foreach (var post in operation.PostAnalyzers)
        {
            outcomes.Add(new AnalyzerOutcome
            {
                Name = post.Name,
                Phase = "post",
                Passed = result.Success || result.FailedAnalyzer != post.Name,
                Message = result.FailedAnalyzer == post.Name ? result.ErrorMessage : null
            });
        }

        return outcomes;
    }
}

/// <summary>
/// امتدادات OpEngine لتنفيذ عملية وإرجاع OperationEnvelope مباشرة.
/// </summary>
public static class OpEngineEnvelopeExtensions
{
    public static async Task<OperationEnvelope<T>> ExecuteEnvelopeAsync<T>(
        this OpEngine engine,
        Operation op,
        T data,
        CancellationToken ct = default)
    {
        var result = await engine.ExecuteAsync(op, ct);
        return OperationEnvelopeFactory.From(op, result, data);
    }

    public static async Task<OperationEnvelope<T>> ExecuteEnvelopeAsync<T>(
        this OpEngine engine,
        Operation op,
        Func<OperationContext, T> dataExtractor,
        CancellationToken ct = default)
    {
        var result = await engine.ExecuteAsync(op, ct);
        var data = result.Success && result.Context != null ? dataExtractor(result.Context) : default!;
        return OperationEnvelopeFactory.From(op, result, data);
    }
}
