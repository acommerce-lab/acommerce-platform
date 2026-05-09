namespace ACommerce.OperationEngine.Analyzers;

using ACommerce.OperationEngine.Core;

/// <summary>
/// محلل عام: يطبق دالة async ويُرجع pass/fail.
/// أبسط طريقة لتحويل lambda إلى محلل بدون إنشاء صنف لكل حالة.
///
/// مثال:
///   .Analyze(new PredicateAnalyzer("non_empty_content",
///       async ctx => string.IsNullOrWhiteSpace(content)
///           ? AnalyzerResult.Fail("empty_content")
///           : AnalyzerResult.Pass()))
/// </summary>
public class PredicateAnalyzer : IOperationAnalyzer
{
    private readonly Func<OperationContext, Task<AnalyzerResult>> _check;
    private readonly IReadOnlyList<string> _watched;

    public string Name { get; }
    public IReadOnlyList<string> WatchedTagKeys => _watched;

    public PredicateAnalyzer(
        string name,
        Func<OperationContext, Task<AnalyzerResult>> check,
        params string[] watchedTagKeys)
    {
        Name = name;
        _check = check;
        _watched = watchedTagKeys;
    }

    public PredicateAnalyzer(
        string name,
        Func<OperationContext, AnalyzerResult> check,
        params string[] watchedTagKeys)
        : this(name, ctx => Task.FromResult(check(ctx)), watchedTagKeys)
    {
    }

    public Task<AnalyzerResult> AnalyzeAsync(OperationContext context) => _check(context);
}

/// <summary>
/// محلل: يفشل إذا كان النص المُمرّر فارغاً أو null.
/// </summary>
public class RequiredFieldAnalyzer : IOperationAnalyzer
{
    private readonly string _fieldName;
    private readonly Func<string?> _valueAccessor;

    public string Name => $"RequiredField({_fieldName})";
    public IReadOnlyList<string> WatchedTagKeys => Array.Empty<string>();

    public RequiredFieldAnalyzer(string fieldName, Func<string?> valueAccessor)
    {
        _fieldName = fieldName;
        _valueAccessor = valueAccessor;
    }

    public Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
    {
        var value = _valueAccessor();
        if (string.IsNullOrWhiteSpace(value))
            return Task.FromResult(AnalyzerResult.Fail($"{_fieldName}_required"));
        return Task.FromResult(AnalyzerResult.Pass());
    }
}

/// <summary>
/// محلل: يفشل إذا كانت القيمة الرقمية خارج النطاق المسموح.
/// </summary>
public class RangeAnalyzer : IOperationAnalyzer
{
    private readonly string _fieldName;
    private readonly Func<decimal> _valueAccessor;
    private readonly decimal? _min;
    private readonly decimal? _max;

    public string Name => $"Range({_fieldName})";
    public IReadOnlyList<string> WatchedTagKeys => Array.Empty<string>();

    public RangeAnalyzer(string fieldName, Func<decimal> valueAccessor, decimal? min = null, decimal? max = null)
    {
        _fieldName = fieldName;
        _valueAccessor = valueAccessor;
        _min = min;
        _max = max;
    }

    public Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
    {
        var value = _valueAccessor();
        if (_min.HasValue && value < _min.Value)
            return Task.FromResult(AnalyzerResult.Fail($"{_fieldName}_below_min: {value} < {_min}"));
        if (_max.HasValue && value > _max.Value)
            return Task.FromResult(AnalyzerResult.Fail($"{_fieldName}_above_max: {value} > {_max}"));
        return Task.FromResult(AnalyzerResult.Pass());
    }
}

/// <summary>
/// محلل: يفشل إذا فشل الـ predicate المُمرّر.
/// </summary>
public class ConditionAnalyzer : IOperationAnalyzer
{
    private readonly string _ruleName;
    private readonly Func<OperationContext, bool> _predicate;
    private readonly string _failMessage;

    public string Name => $"Condition({_ruleName})";
    public IReadOnlyList<string> WatchedTagKeys => Array.Empty<string>();

    public ConditionAnalyzer(string ruleName, Func<OperationContext, bool> predicate, string failMessage)
    {
        _ruleName = ruleName;
        _predicate = predicate;
        _failMessage = failMessage;
    }

    public Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
    {
        return Task.FromResult(_predicate(context)
            ? AnalyzerResult.Pass()
            : AnalyzerResult.Fail(_failMessage));
    }
}

/// <summary>
/// محلل: يفشل إذا تجاوز الطول حداً معيّناً.
/// </summary>
public class MaxLengthAnalyzer : IOperationAnalyzer
{
    private readonly string _fieldName;
    private readonly Func<string?> _valueAccessor;
    private readonly int _maxLength;

    public string Name => $"MaxLength({_fieldName})";
    public IReadOnlyList<string> WatchedTagKeys => Array.Empty<string>();

    public MaxLengthAnalyzer(string fieldName, Func<string?> valueAccessor, int maxLength)
    {
        _fieldName = fieldName;
        _valueAccessor = valueAccessor;
        _maxLength = maxLength;
    }

    public Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
    {
        var value = _valueAccessor();
        if (value != null && value.Length > _maxLength)
            return Task.FromResult(AnalyzerResult.Fail(
                $"{_fieldName}_too_long: {value.Length} > {_maxLength}"));
        return Task.FromResult(AnalyzerResult.Pass());
    }
}
