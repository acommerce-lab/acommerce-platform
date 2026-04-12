namespace ACommerce.OperationEngine.Core;

/// <summary>
/// باني العمليات السلس.
///
/// مثال بسيط:
///   Op.Create("send.message")
///     .Party("User:ahmed", tag: ("direction","debit"))
///     .Party("User:sara", tag: ("direction","credit"))
///     .Tag("category", "chat")
///     .Analyze(new BalanceAnalyzer())
///     .Execute(ctx => SendMessage(ctx))
///     .Build();
///
/// مثال مُركّب:
///   Op.Create("sale")
///     .Party("Buyer:123", 100m, ("direction","debit"), ("cost_center","retail"))
///     .Party("Seller:456", 100m, ("direction","credit"), ("cost_center","wholesale"))
///     .Tag("category", "sales")
///     .Tag("workflow", "step_1")
///     .Analyze(new BalanceAnalyzer())
///     .Analyze(new SequenceAnalyzer(checkStep))
///     .PostAnalyze(new CommissionAnalyzer())
///     .OnAfterExecute(ctx => db.SaveAsync(ctx))
///     .WithSub("commission", sub => sub
///         .Party("Seller:456", 5m, ("direction","debit"))
///         .Party("Platform", 5m, ("direction","credit")))
///     .Build();
/// </summary>
public static class Op
{
    public static OperationBuilder Create(string type) => new(type);
}

public class OperationBuilder
{
    private readonly Operation _op;

    internal OperationBuilder(string type) => _op = new Operation(type);

    // === الوصف ===
    public OperationBuilder Describe(string desc) { _op.Description = desc; return this; }

    // === الأطراف ===
    public OperationBuilder Party(string identity, decimal value = 0, params (string Key, string Value)[] tags)
    {
        var party = new Party(identity, value);
        foreach (var (k, v) in tags) party.AddTag(k, v);
        _op.AddParty(party);
        return this;
    }

    // === العلامات على العملية ===
    public OperationBuilder Tag(string key, string value) { _op.AddTag(key, value); return this; }

    // === العلاقة مع عملية أصلية ===
    public OperationBuilder RelatedTo(Guid originalId, OperationRelation relation)
    {
        _op.OriginalOperationId = originalId;
        _op.Relation = relation;
        _op.AddTag("relation", relation.ToString().ToLower());
        return this;
    }

    // === عقود المزودين ===
    /// <summary>
    /// يُصرّح أن هذه العملية تحتاج إلى مزود من نوع T مسجّلاً في DI.
    /// يتحقق المحرك من وجوده قبل التنفيذ.
    /// </summary>
    public OperationBuilder Requires<T>() { _op.AddRequiredContract(typeof(T)); return this; }

    // === المحللات ===
    public OperationBuilder Analyze(IOperationAnalyzer analyzer) { _op.AddPreAnalyzer(analyzer); return this; }
    public OperationBuilder PostAnalyze(IOperationAnalyzer analyzer) { _op.AddPostAnalyzer(analyzer); return this; }

    // === دورة الحياة ===
    public OperationBuilder Validate(Func<OperationContext, Task<bool>> fn) { _op.ValidateFunc = fn; return this; }
    public OperationBuilder Validate(Func<OperationContext, bool> fn) { _op.ValidateFunc = ctx => Task.FromResult(fn(ctx)); return this; }
    public OperationBuilder Execute(Func<OperationContext, Task> fn) { _op.ExecuteFunc = fn; return this; }
    public OperationBuilder Execute(Action<OperationContext> fn) { _op.ExecuteFunc = ctx => { fn(ctx); return Task.CompletedTask; }; return this; }
    public OperationBuilder PostValidate(Func<OperationContext, Task> fn) { _op.PostValidateFunc = fn; return this; }

    // === Hooks ===
    public OperationBuilder OnBeforeAnalyze(Func<OperationContext, Task> h) { _op.Hooks.BeforeAnalyze = h; return this; }
    public OperationBuilder OnAfterAnalyze(Func<OperationContext, Task> h) { _op.Hooks.AfterAnalyze = h; return this; }
    public OperationBuilder OnBeforeValidate(Func<OperationContext, Task> h) { _op.Hooks.BeforeValidate = h; return this; }
    public OperationBuilder OnAfterValidate(Func<OperationContext, Task> h) { _op.Hooks.AfterValidate = h; return this; }
    public OperationBuilder OnBeforeExecute(Func<OperationContext, Task> h) { _op.Hooks.BeforeExecute = h; return this; }
    public OperationBuilder OnAfterExecute(Func<OperationContext, Task> h) { _op.Hooks.AfterExecute = h; return this; }
    public OperationBuilder OnBeforeComplete(Func<OperationContext, Task> h) { _op.Hooks.BeforeComplete = h; return this; }
    public OperationBuilder OnAfterComplete(Func<OperationContext, Task> h) { _op.Hooks.AfterComplete = h; return this; }
    public OperationBuilder OnBeforeFail(Func<OperationContext, Task> h) { _op.Hooks.BeforeFail = h; return this; }
    public OperationBuilder OnAfterFail(Func<OperationContext, Task> h) { _op.Hooks.AfterFail = h; return this; }

    // === العمليات الفرعية ===
    public OperationBuilder WithSub(string type, Action<OperationBuilder> configure)
    {
        var sub = new OperationBuilder(type);
        configure(sub);
        _op.AddSubOperation(sub._op);
        return this;
    }

    public OperationBuilder WithSubs<T>(IEnumerable<T> items, Func<T, string> typeSelector, Action<OperationBuilder, T> configure)
    {
        foreach (var item in items)
        {
            var sub = new OperationBuilder(typeSelector(item));
            configure(sub, item);
            _op.AddSubOperation(sub._op);
        }
        return this;
    }

    // === بيانات وصفية ===
    public OperationBuilder Meta(string key, object value) { _op.Metadata[key] = value; return this; }

    // === البناء ===
    public Operation Build() => _op;
}
