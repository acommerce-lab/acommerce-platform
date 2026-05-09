namespace ACommerce.OperationEngine.Core;

/// <summary>
/// العملية: الوحدة الذرية في النظام.
/// = أطراف + علامات + محللات + أحداث دورة الحياة + عمليات فرعية.
///
/// لا تعرف ما هو "قيد محاسبي" أو "إشعار".
/// هذه مجرد عملية بين أطراف لهم علامات.
/// </summary>
public class Operation : ITaggable
{
    private readonly TagCollection _tags = new();
    private readonly List<Party> _parties = new();
    private readonly List<Operation> _subOperations = new();
    private readonly List<IOperationAnalyzer> _preAnalyzers = new();
    private readonly List<IOperationAnalyzer> _postAnalyzers = new();
    private readonly List<Type> _requiredContracts = new();

    public Guid Id { get; } = Guid.NewGuid();
    public string Type { get; }
    public string? Description { get; set; }
    public OperationStatus Status { get; internal set; } = OperationStatus.Created;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; internal set; }
    public Guid? ParentOperationId { get; internal set; }

    /// <summary>
    /// مرجع عملية أصلية (للمعكوسات والتمام)
    /// </summary>
    public Guid? OriginalOperationId { get; set; }
    public OperationRelation? Relation { get; set; }

    // === الأطراف ===
    public IReadOnlyList<Party> Parties => _parties;

    // === العمليات الفرعية ===
    public IReadOnlyList<Operation> SubOperations => _subOperations;

    // === المحللات ===
    public IReadOnlyList<IOperationAnalyzer> PreAnalyzers => _preAnalyzers;
    public IReadOnlyList<IOperationAnalyzer> PostAnalyzers => _postAnalyzers;

    // === عقود المزودين المطلوبة ===
    public IReadOnlyList<Type> RequiredContracts => _requiredContracts;

    // === دوال التنفيذ (يحددها الباني) ===
    internal Func<OperationContext, Task<bool>>? ValidateFunc { get; set; }
    internal Func<OperationContext, Task>? ExecuteFunc { get; set; }
    internal Func<OperationContext, Task>? PostValidateFunc { get; set; }

    // === أحداث دورة الحياة ===
    public OperationLifecycleHooks Hooks { get; } = new();

    // === بيانات وصفية ===
    public Dictionary<string, object> Metadata { get; } = new();

    public Operation(string type) => Type = type;

    // === بناء ===
    public void AddParty(Party party) => _parties.Add(party);
    public void AddSubOperation(Operation sub) { sub.ParentOperationId = Id; _subOperations.Add(sub); }
    public void AddPreAnalyzer(IOperationAnalyzer analyzer) => _preAnalyzers.Add(analyzer);
    public void AddPostAnalyzer(IOperationAnalyzer analyzer) => _postAnalyzers.Add(analyzer);
    public void AddRequiredContract(Type contractType) { if (!_requiredContracts.Contains(contractType)) _requiredContracts.Add(contractType); }

    // === ITaggable ===
    public IReadOnlyList<Tag> Tags => _tags.Tags;
    public void AddTag(string key, string value) => _tags.AddTag(key, value);
    public void RemoveTag(string key, string? value = null) => _tags.RemoveTag(key, value);
    public string? GetTagValue(string key) => _tags.GetTagValue(key);
    public IEnumerable<string> GetTagValues(string key) => _tags.GetTagValues(key);
    public bool HasTag(string key, string? value = null) => _tags.HasTag(key, value);

    /// <summary>
    /// الحصول على أطراف بعلامة محددة
    /// </summary>
    public IEnumerable<Party> GetPartiesByTag(string key, string? value = null) =>
        _parties.Where(p => p.HasTag(key, value));
}

public enum OperationStatus
{
    Created,
    Analyzing,
    Validated,
    Executing,
    Completed,
    PartiallyCompleted,
    Failed,
    Reversed,
    Cancelled
}

/// <summary>
/// علاقة بين عمليتين
/// </summary>
public enum OperationRelation
{
    /// <summary>
    /// تمام كلي: هذه العملية تُكمل الأصلية بالكامل
    /// </summary>
    Fulfillment,

    /// <summary>
    /// تمام جزئي: هذه العملية تُكمل جزءاً من الأصلية
    /// </summary>
    PartialFulfillment,

    /// <summary>
    /// معكوس كلي: هذه العملية تعكس الأصلية بالكامل
    /// </summary>
    Reversal,

    /// <summary>
    /// تعديل: هذه العملية تعدّل الأصلية جزئياً
    /// </summary>
    Amendment
}
