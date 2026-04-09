namespace ACommerce.OperationEngine.Core;

/// <summary>
/// طرف في عملية. لا نفترض أنه "دائن" أو "مدين" -
/// هذه علامات يضيفها النمط المحاسبي إن أراد.
///
/// الطرف = هوية + علامات + قيمة اختيارية.
/// </summary>
public class Party : ITaggable
{
    private readonly TagCollection _tags = new();

    /// <summary>
    /// هوية الطرف (مثل: "User:123", "System", "Channel:Email")
    /// </summary>
    public string Identity { get; }

    /// <summary>
    /// قيمة رقمية اختيارية (للعمليات الكمية)
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// حالة الطرف في دورة الحياة
    /// </summary>
    public PartyStatus Status { get; set; } = PartyStatus.Pending;

    /// <summary>
    /// بيانات إضافية
    /// </summary>
    public Dictionary<string, object> Payload { get; } = new();

    public Party(string identity, decimal value = 0)
    {
        Identity = identity;
        Value = value;
    }

    // === ITaggable ===
    public IReadOnlyList<Tag> Tags => _tags.Tags;
    public void AddTag(string key, string value) => _tags.AddTag(key, value);
    public void RemoveTag(string key, string? value = null) => _tags.RemoveTag(key, value);
    public string? GetTagValue(string key) => _tags.GetTagValue(key);
    public IEnumerable<string> GetTagValues(string key) => _tags.GetTagValues(key);
    public bool HasTag(string key, string? value = null) => _tags.HasTag(key, value);

    public override string ToString() => $"{Identity}({Value}) {string.Join("", Tags)}";
}

public enum PartyStatus
{
    Pending,
    Active,
    Completed,
    Failed,
    Cancelled
}
