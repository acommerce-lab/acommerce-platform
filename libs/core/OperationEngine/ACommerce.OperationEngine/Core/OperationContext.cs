using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// يحلّ عقد مزوّد مُعلَن مسبقاً بـ Requires&lt;T&gt;() في الباني.
    /// اختصار واضح الاسم بدلاً من GetService مباشرةً داخل Execute.
    /// </summary>
    public T Provider<T>() where T : notnull => Services.GetRequiredService<T>();
    public void Set<T>(string key, T value) => Items[key] = value!;
    public T Get<T>(string key) => (T)Items[key];
    public bool TryGet<T>(string key, out T? value)
    {
        if (Items.TryGetValue(key, out var obj) && obj is T typed) { value = typed; return true; }
        value = default; return false;
    }

    // ─── Entity carriers (F1 — typed entity flow through the operation) ──
    // الكيان (مثل IChatMessage، IReport، IBookingDraft) يُمرَّر داخل القيد
    // كـ instance يُحقّق interface المكتبة المعنيّة. interceptors تستهلكه
    // typed بدل أن تجمع tags وتعيد تركيب الـ entity. القانون السادس في
    // CLAUDE.md (واجهات لا DTOs) — هذا هو التطبيق العمليّ على مستوى القيد.
    //
    // المفتاح يستخدم اسم النوع الكامل، فالـ entity من نوع I سيُجلَب لاحقاً
    // عبر <I>؛ يستطيع interceptor وضع entity أساسيّ والمستهلك أن يطلبه
    // عبر interface فرعيّ — يعمل ما دامت العلاقة <T : IBase> صحيحة.
    /// <summary>يضع entity مكتَّبة على القيد. أيّ interceptor لاحق يستطيع
    /// طلبها عبر <see cref="Entity{TEntity}"/>.</summary>
    public OperationContext WithEntity<TEntity>(TEntity entity) where TEntity : class
    {
        Items[EntityKey<TEntity>()] = entity;
        return this;
    }

    /// <summary>يجلب الـ entity المسجَّلة مسبقاً، أو null لو لم تُسجَّل.</summary>
    public TEntity? Entity<TEntity>() where TEntity : class
    {
        return Items.TryGetValue(EntityKey<TEntity>(), out var obj) ? obj as TEntity : null;
    }

    /// <summary>يجلب الـ entity أو يرمي — لمعترضات تشترط وجودها.</summary>
    public TEntity RequireEntity<TEntity>() where TEntity : class
    {
        return Entity<TEntity>()
            ?? throw new InvalidOperationException(
                $"Operation '{Operation.Type}' لا يحمل entity من نوع {typeof(TEntity).FullName}. " +
                $"تأكّد أنّ الـ builder استدعى .WithEntity<{typeof(TEntity).Name}>(...) أو أنّ Execute body وضعها عبر ctx.WithEntity(...).");
    }

    private static string EntityKey<T>() => "_entity:" + typeof(T).FullName;

    public void AddValidationError(string error)
    {
        if (!Items.ContainsKey("_errors")) Items["_errors"] = new List<string>();
        ((List<string>)Items["_errors"]).Add(error);
    }

    public void AddValidationError(string field, string message) => AddValidationError($"{field}: {message}");
    public List<string> GetValidationErrors() => Items.TryGetValue("_errors", out var e) ? (List<string>)e : new();
}
