namespace ACommerce.Cache.Operations.Abstractions;

/// <summary>
/// عقد التخزين المؤقّت. مفتاح-قيمة مع TTL وعمليّات ذرّيّة.
/// المزوّدات: <c>InMemory</c> للتطوير، <c>Redis</c> للإنتاج وللحالة الموزَّعة بين عمليّات backend.
///
/// <para>الـ Serialization مسؤوليّة المزوّد. الـ <c>T</c> يُسلسَل JSON عادةً (ما لم يكن
/// <c>string</c> أو bytes — تُكتب خام). لا تستعمل أنواعاً تحوي مراجع دائريّة.</para>
/// </summary>
public interface ICache
{
    /// <summary>يقرأ القيمة. <c>null</c> إن لم توجد أو انتهت مدّتها.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>يكتب القيمة (يستبدل أيّ قيمة سابقة).</summary>
    Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default);

    /// <summary>يحذف المفتاح. يرجع <c>true</c> إن وُجد قبل الحذف.</summary>
    Task<bool> RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>هل المفتاح موجود وغير منتهي؟</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// زيادة ذرّيّة لقيمة عدديّة. يُنشئ المفتاح بقيمة <paramref name="delta"/> إن لم يوجد.
    /// مفيد للعدّادات (rate limit, stock counter…).
    /// </summary>
    Task<long> IncrementAsync(string key, long delta = 1, CancellationToken ct = default);

    /// <summary>
    /// كتابة شَرطيّة (<c>SET ... NX</c>): يكتب القيمة إن لم يوجد المفتاح، ويرجع <c>true</c>؛
    /// إن وُجد لم يكتب ويرجع <c>false</c>. أساس البناء للأقفال البسيطة.
    /// </summary>
    Task<bool> SetIfNotExistsAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// النمط الأشيع: قراءة، وإن لم توجد ينفّذ <paramref name="factory"/> ويكتب النتيجة قبل الإرجاع.
    /// لا يحمي من thundering-herd بطبيعته — أضف <see cref="AcquireLockAsync"/> حوله إن احتجت.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// محاولة الحصول على قفل موزَّع. يرجع handle قابل للتخلّص يحرّر القفل، أو <c>null</c>
    /// إن كان مكتسَباً من طرف آخر. <paramref name="ttl"/> يضمن تحرير القفل تلقائيّاً
    /// لو انهار الحاصل عليه.
    /// </summary>
    Task<IDistributedLock?> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct = default);
}
