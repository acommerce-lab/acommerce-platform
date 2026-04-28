namespace ACommerce.OperationEngine.Core;

/// <summary>
/// علامة: مفتاح + قيمة.
/// العلامة هي الوحدة الذرية في النظام.
/// كل شيء آخر (قيد محاسبي، تدفق عمليات، إشعار) = نمط من العلامات.
/// </summary>
public readonly record struct Tag(string Key, string Value)
{
    public override string ToString() => $"[{Key}:{Value}]";
}

/// <summary>
/// أي شيء يمكن وسمه بعلامات: العملية، الطرف، القيد الفرعي.
/// </summary>
public interface ITaggable
{
    IReadOnlyList<Tag> Tags { get; }
    void AddTag(string key, string value);
    void RemoveTag(string key, string? value = null);
    string? GetTagValue(string key);
    IEnumerable<string> GetTagValues(string key);
    bool HasTag(string key, string? value = null);
}

/// <summary>
/// تطبيق ITaggable القابل لإعادة الاستخدام
/// </summary>
public class TagCollection : ITaggable
{
    private readonly List<Tag> _tags = new();
    public IReadOnlyList<Tag> Tags => _tags;

    public void AddTag(string key, string value) => _tags.Add(new Tag(key, value));

    public void RemoveTag(string key, string? value = null)
    {
        if (value == null)
            _tags.RemoveAll(t => t.Key == key);
        else
            _tags.RemoveAll(t => t.Key == key && t.Value == value);
    }

    public string? GetTagValue(string key) => _tags.FirstOrDefault(t => t.Key == key).Value;

    public IEnumerable<string> GetTagValues(string key) => _tags.Where(t => t.Key == key).Select(t => t.Value);

    public bool HasTag(string key, string? value = null) =>
        value == null ? _tags.Any(t => t.Key == key) : _tags.Any(t => t.Key == key && t.Value == value);
}
