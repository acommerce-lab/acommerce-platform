using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;

namespace ACommerce.OperationEngine.Patterns;

/// <summary>
/// النمط المحاسبي (Layer 1) - واجهة المطور للتفكير المحاسبي.
///
/// يُخفي العلامات والمحللات خلف واجهة مألوفة:
///   Entry.Create("sale")
///     .From("Buyer:123", 100)
///     .To("Seller:456", 100)
///     .Execute(ctx => ...)
///     .Build();
///
/// تحت الغطاء:
///   Operation + Party[direction:debit] + Party[direction:credit] + BalanceAnalyzer
///
/// هذا النمط يفرض:
///   - وجود طرف مدين (From) وطرف دائن (To) على الأقل
///   - التوازن التلقائي (BalanceAnalyzer مُضاف دائماً)
///   - علامة [pattern:accounting] على كل عملية
///
/// ولا يمنع:
///   - إضافة علامات إضافية (cost_center, workflow, etc.)
///   - إضافة محللات إضافية
///   - إضافة hooks
///   - كل ما تقدمه الطبقة 0
/// </summary>
public static class Entry
{
    public static AccountingBuilder Create(string type) => new(type);

    /// <summary>Type-safe overload باستخدام OperationType</summary>
    public static AccountingBuilder Create(OperationType type) => new(type.Name);
}

public class AccountingBuilder
{
    private readonly OperationBuilder _inner;
    private bool _balanceAdded;

    internal AccountingBuilder(string type)
    {
        _inner = Op.Create(type);
        _inner.Tag("pattern", "accounting");
    }

    // =========================================================================
    // الواجهة المحاسبية: From/To (مدين/دائن)
    // =========================================================================

    /// <summary>
    /// الطرف المدين (المُرسل/المُعطي/المُصدر)
    /// </summary>
    public AccountingBuilder From(string identity, decimal value = 1, params (string Key, string Value)[] extraTags)
    {
        var tags = new List<(string, string)> { ("direction", "debit") };
        tags.AddRange(extraTags);
        _inner.Party(identity, value, tags.ToArray());
        EnsureBalance();
        return this;
    }

    /// <summary>
    /// الطرف الدائن (المُستلم/الحاصل)
    /// </summary>
    public AccountingBuilder To(string identity, decimal value = 1, params (string Key, string Value)[] extraTags)
    {
        var tags = new List<(string, string)> { ("direction", "credit") };
        tags.AddRange(extraTags);
        _inner.Party(identity, value, tags.ToArray());
        EnsureBalance();
        return this;
    }

    /// <summary>
    /// أطراف مدينون متعددون (من كثير)
    /// </summary>
    public AccountingBuilder FromMany(IEnumerable<(string Identity, decimal Value)> parties)
    {
        foreach (var (id, val) in parties)
            _inner.Party(id, val, ("direction", "debit"));
        EnsureBalance();
        return this;
    }

    /// <summary>
    /// أطراف دائنون متعددون (إلى كثير)
    /// </summary>
    public AccountingBuilder ToMany(IEnumerable<(string Identity, decimal Value)> parties)
    {
        foreach (var (id, val) in parties)
            _inner.Party(id, val, ("direction", "credit"));
        EnsureBalance();
        return this;
    }

    // =========================================================================
    // المعكوسات والعلاقات
    // =========================================================================

    /// <summary>
    /// هذه العملية تُكمل (تمام) عملية سابقة
    /// </summary>
    public AccountingBuilder Fulfills(Guid originalId)
    {
        _inner.RelatedTo(originalId, OperationRelation.Fulfillment);
        return this;
    }

    /// <summary>
    /// هذه العملية تُكمل جزءاً من عملية سابقة
    /// </summary>
    public AccountingBuilder PartiallyFulfills(Guid originalId)
    {
        _inner.RelatedTo(originalId, OperationRelation.PartialFulfillment);
        return this;
    }

    /// <summary>
    /// هذه العملية تعكس عملية سابقة بالكامل
    /// </summary>
    public AccountingBuilder Reverses(Guid originalId)
    {
        _inner.RelatedTo(originalId, OperationRelation.Reversal);
        return this;
    }

    /// <summary>
    /// هذه العملية تعدّل عملية سابقة
    /// </summary>
    public AccountingBuilder Amends(Guid originalId)
    {
        _inner.RelatedTo(originalId, OperationRelation.Amendment);
        return this;
    }

    /// <summary>
    /// إضافة محلل المعكوسات (يتحقق من وجود الأصلية)
    /// </summary>
    public AccountingBuilder WithFulfillmentCheck(Func<Guid, Task<bool>> checkOriginal)
    {
        _inner.Analyze(new FulfillmentAnalyzer(checkOriginal));
        return this;
    }

    // =========================================================================
    // العلامات الإضافية
    // =========================================================================

    /// <summary>
    /// علامة مركز التكلفة
    /// </summary>
    public AccountingBuilder CostCenter(string center)
    {
        _inner.Tag("cost_center", center);
        return this;
    }

    /// <summary>
    /// علامة تدفق العمليات
    /// </summary>
    public AccountingBuilder WorkflowStep(string step)
    {
        _inner.Tag("workflow", step);
        return this;
    }

    /// <summary>
    /// علامة تصنيف حرة
    /// </summary>
    public AccountingBuilder Tag(string key, string value)
    {
        _inner.Tag(key, value);
        return this;
    }

    /// <summary>Type-safe overload: TagKey + string value</summary>
    public AccountingBuilder Tag(TagKey key, string value) => Tag(key.Name, value);

    /// <summary>Type-safe overload: TagKey + TagValue</summary>
    public AccountingBuilder Tag(TagKey key, TagValue value) => Tag(key.Name, value.Value);

    /// <summary>Type-safe overload: TagKey + Guid</summary>
    public AccountingBuilder Tag(TagKey key, Guid value) => Tag(key.Name, value.ToString());

    /// <summary>
    /// يختم القيد - يمنع كل المعترضات العامة من الحقن.
    /// مفيد للقيود الحساسة التي يجب ألا تتأثر بأي طبقة cross-cutting.
    /// </summary>
    public AccountingBuilder Sealed()
    {
        _inner.Tag("sealed", "true");
        return this;
    }

    /// <summary>
    /// يستثني معترضاً معيناً بالاسم من الحقن في هذا القيد.
    /// </summary>
    public AccountingBuilder ExcludeInterceptor(string interceptorName)
    {
        _inner.Tag("exclude_interceptor", interceptorName);
        return this;
    }

    // =========================================================================
    // عقود المزودين
    // =========================================================================

    /// <summary>
    /// يُصرّح أن هذا القيد يحتاج إلى مزود من نوع T مسجّلاً في DI.
    /// يتحقق المحرك من وجوده قبل التنفيذ.
    /// </summary>
    public AccountingBuilder Requires<T>() { _inner.Requires<T>(); return this; }

    // =========================================================================
    // المحللات الإضافية
    // =========================================================================

    /// <summary>
    /// إضافة محلل سابق (يعمل قبل التنفيذ)
    /// </summary>
    public AccountingBuilder Analyze(IOperationAnalyzer analyzer)
    {
        _inner.Analyze(analyzer);
        return this;
    }

    /// <summary>
    /// إضافة محلل لاحق (يعمل بعد التنفيذ)
    /// </summary>
    public AccountingBuilder PostAnalyze(IOperationAnalyzer analyzer)
    {
        _inner.PostAnalyze(analyzer);
        return this;
    }

    // =========================================================================
    // دورة الحياة
    // =========================================================================

    public AccountingBuilder Describe(string desc) { _inner.Describe(desc); return this; }

    public AccountingBuilder Validate(Func<OperationContext, Task<bool>> fn) { _inner.Validate(fn); return this; }
    public AccountingBuilder Validate(Func<OperationContext, bool> fn) { _inner.Validate(fn); return this; }

    public AccountingBuilder Execute(Func<OperationContext, Task> fn) { _inner.Execute(fn); return this; }
    public AccountingBuilder Execute(Action<OperationContext> fn) { _inner.Execute(fn); return this; }

    // === Hooks ===
    public AccountingBuilder OnBeforeValidate(Func<OperationContext, Task> h) { _inner.OnBeforeValidate(h); return this; }
    public AccountingBuilder OnAfterValidate(Func<OperationContext, Task> h) { _inner.OnAfterValidate(h); return this; }
    public AccountingBuilder OnBeforeExecute(Func<OperationContext, Task> h) { _inner.OnBeforeExecute(h); return this; }
    public AccountingBuilder OnAfterExecute(Func<OperationContext, Task> h) { _inner.OnAfterExecute(h); return this; }
    public AccountingBuilder OnBeforeComplete(Func<OperationContext, Task> h) { _inner.OnBeforeComplete(h); return this; }
    public AccountingBuilder OnAfterComplete(Func<OperationContext, Task> h) { _inner.OnAfterComplete(h); return this; }
    public AccountingBuilder OnBeforeFail(Func<OperationContext, Task> h) { _inner.OnBeforeFail(h); return this; }
    public AccountingBuilder OnAfterFail(Func<OperationContext, Task> h) { _inner.OnAfterFail(h); return this; }

    // === العمليات الفرعية (بنمط محاسبي) ===
    public AccountingBuilder WithSubEntry(string type, Action<AccountingBuilder> configure)
    {
        var sub = new AccountingBuilder(type);
        configure(sub);
        _inner.WithSub(type, _ => { }); // placeholder - نستبدله
        // نحتاج وصول مباشر للعملية الفرعية
        return this;
    }

    /// <summary>
    /// إضافة عملية فرعية مباشرة (مرونة كاملة)
    /// </summary>
    public AccountingBuilder WithSub(string type, Action<OperationBuilder> configure)
    {
        _inner.WithSub(type, configure);
        return this;
    }

    /// <summary>
    /// إضافة عمليات فرعية ديناميكياً
    /// </summary>
    public AccountingBuilder WithSubEntries<T>(IEnumerable<T> items, Func<T, string> typeSelector, Action<AccountingBuilder, T> configure)
    {
        foreach (var item in items)
        {
            var sub = new AccountingBuilder(typeSelector(item));
            configure(sub, item);
            // مؤقتاً: نستخدم Build ثم نضيف
        }
        return this;
    }

    // === بيانات وصفية ===
    public AccountingBuilder Meta(string key, object value) { _inner.Meta(key, value); return this; }

    // =========================================================================
    // البناء
    // =========================================================================

    private void EnsureBalance()
    {
        if (!_balanceAdded)
        {
            _inner.Analyze(new BalanceAnalyzer());
            _balanceAdded = true;
        }
    }

    /// <summary>
    /// بناء العملية النهائية.
    /// يُرجع Operation من الطبقة 0 - يمكن تنفيذها بـ OpEngine.
    /// </summary>
    public Operation Build() => _inner.Build();
}
