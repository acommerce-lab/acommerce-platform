namespace ACommerce.OperationEngine.Interceptors;

/// <summary>
/// مُعتَرِض idempotency عامّ — يَمنَع تَنفيذ نَفس العَمَلِيَّة الكِتابِيَّة
/// مَرَّتَين لَو حَمَلَت نَفس <c>idempotency_key</c> tag.
///
/// <para><b>المُشكِلَة الَّتي يَحُلّها</b>: الكلاينت يُرسِل <c>POST /my-listings</c>،
/// الخَادِم يَحفَظ + يَرُدّ نَجاحاً، لكِنّ HTTP response يَضيع (شَبَكَة، timeout،
/// browser).  المُستَخدِم يَعتَقِد فَشِل ⇒ يُكَرِّر ⇒ ٥ نُسَخ.</para>
///
/// <para><b>الحَلّ</b>: الكلاينت يُوَلِّد <see cref="Guid"/> فَريد لِكُلّ
/// "نِيَّة كِتابَة" + يَستَخدِم نَفس الـ Guid في كُلّ إعادَة مُحاوَلَة. الـ
/// interceptor يَفحَص قَبل التَّنفيذ: لَو سُجِّلَ نَفس الـ key سابِقاً
/// (نَجَح أَو فَشَل) ⇒ يَرُدّ نَفس النَّتيجَة بِلا إعادَة تَنفيذ.</para>
///
/// <para><b>التَّخزين</b>: <see cref="IOperationIdempotencyStore"/> يُنَفِّذه
/// التَطبيق (عادَةً EF + جَدول <c>OperationIdempotency</c>). TTL مُقتَرَح:
/// ٢٤ ساعَة.</para>
///
/// <para><b>كَيف يَستَخدِمه التَطبيق</b>:
/// <code>
/// // عَلى الكلاينت — يُمَرَّر كَ tag:
/// Entry.Create("listing.create")
///      .Tag(OperationTagKeys.IdempotencyKey, Guid.NewGuid().ToString())
///      .Execute(...)
///
/// // عَلى الخَادِم — تَسجيل المُعتَرِض في DI مَرَّة واحِدَة:
/// services.AddSingleton&lt;IOperationInterceptor, IdempotencyInterceptor&gt;();
/// services.AddScoped&lt;IOperationIdempotencyStore, EfOperationIdempotencyStore&gt;();
/// </code></para>
/// </summary>
public sealed class IdempotencyInterceptor : IOperationInterceptor
{
    public string Name => "operation_idempotency";
    public InterceptorPhase Phase => InterceptorPhase.Both;

    public bool AppliesTo(Core.Operation op)
        => op.GetTagValue(OperationTagKeys.IdempotencyKey) is { Length: > 0 };

    public async Task<Core.AnalyzerResult> InterceptAsync(
        Core.OperationContext context, Core.OperationResult? result = null)
    {
        var key = context.Operation.GetTagValue(OperationTagKeys.IdempotencyKey);
        if (string.IsNullOrWhiteSpace(key)) return Core.AnalyzerResult.Pass();

        var store = context.Services.GetService(typeof(IOperationIdempotencyStore))
                    as IOperationIdempotencyStore;
        if (store is null) return Core.AnalyzerResult.Pass();   // التَطبيق لَم يُسَجِّل ⇒ تَجاوَز

        // Pre-phase (result is null): تَحَقَّق مَن الـ cache. لَو مَوجود ⇒ نَمنَع
        // التَّنفيذ ونَضَع الـ snapshot في Items لِيَعرِفه الـ controller.
        if (result is null)
        {
            var cached = await store.TryGetAsync(key, context.CancellationToken);
            if (cached is not null)
            {
                context.Items["idempotent_replay"] = cached;
                return Core.AnalyzerResult.Fail(
                    "idempotent_replay", blocking: true);
            }
            return Core.AnalyzerResult.Pass();
        }

        // Post-phase (result != null): سَجِّل لَو نَجَح. الفَشَل لا يُسَجَّل
        // — يَجِب أَن يُعيد المُستَخدِم بِبَيانات صَحيحَة.
        if (result.Success)
        {
            await store.SaveAsync(
                key,
                context.Operation.Type,
                snapshot: result.OperationId.ToString(),
                context.CancellationToken);
        }
        return Core.AnalyzerResult.Pass();
    }
}

/// <summary>
/// مُفاتيح tags المُستَخدَمَة في الـ <see cref="IdempotencyInterceptor"/>.
/// مُعَرَّفَة هُنا (لا عَلى مُستَوى OperationEngine.Core) لِأَنّها خاصَّة
/// بِهذا المُعتَرِض.
/// </summary>
public static class OperationTagKeys
{
    public const string IdempotencyKey = "idempotency_key";
}

/// <summary>
/// المَنفَذ الَّذي يُنَفِّذه التَطبيق لِتَخزين الـ idempotency records.
/// عادَةً جَدول EF بَسيط <c>OperationIdempotency(Key, OperationType,
/// Snapshot, CreatedAt)</c> مَع TTL عَبر background job.
/// </summary>
public interface IOperationIdempotencyStore
{
    /// <summary>يَرُدّ snapshot النَجاح السابِق، أَو null لَو الـ key جَديد.</summary>
    Task<IdempotencyRecord?> TryGetAsync(string key, CancellationToken ct);

    /// <summary>يَحفَظ الـ envelope النِهائي بَعد نَجاح أَوَّل تَنفيذ.</summary>
    Task SaveAsync(string key, string operationType, string snapshot, CancellationToken ct);
}

public sealed record IdempotencyRecord(
    string Key,
    string OperationType,
    string Snapshot,
    DateTime CreatedAt);
