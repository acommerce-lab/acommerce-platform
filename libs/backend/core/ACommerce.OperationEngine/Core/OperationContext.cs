namespace ACommerce.OperationEngine.Core;

/// <summary>
/// سياق تنفيذ العملية - يُمرر لكل محلل ودالة ومرحلة.
/// </summary>
public class OperationContext
{
    public Operation Operation { get; }
    public Operation? ParentOperation { get; internal set; }
    public IServiceProvider Services { get; }
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// مخزن مؤقت لتبادل البيانات بين المراحل
    /// </summary>
    public Dictionary<string, object> Items { get; } = new();

    /// <summary>
    /// نتائج المحللات
    /// </summary>
    public List<(string AnalyzerName, AnalyzerResult Result)> AnalyzerResults { get; } = new();

    /// <summary>
    /// كل الأحداث التي أطلقتها المحللات
    /// </summary>
    public List<AnalyzerEvent> AnalyzerEvents { get; } = new();

    public OperationContext(Operation operation, IServiceProvider services, CancellationToken ct = default)
    {
        Operation = operation;
        Services = services;
        CancellationToken = ct;
    }

    public T GetService<T>() where T : notnull => (T)Services.GetService(typeof(T))!;
    public void Set<T>(string key, T value) => Items[key] = value!;
    public T Get<T>(string key) => (T)Items[key];
    public bool TryGet<T>(string key, out T? value)
    {
        if (Items.TryGetValue(key, out var obj) && obj is T typed) { value = typed; return true; }
        value = default; return false;
    }

    public void AddValidationError(string error)
    {
        if (!Items.ContainsKey("_errors")) Items["_errors"] = new List<string>();
        ((List<string>)Items["_errors"]).Add(error);
    }

    public void AddValidationError(string field, string message) => AddValidationError($"{field}: {message}");
    public List<string> GetValidationErrors() => Items.TryGetValue("_errors", out var e) ? (List<string>)e : new();
}
