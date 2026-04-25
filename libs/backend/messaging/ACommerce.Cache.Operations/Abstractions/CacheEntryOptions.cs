namespace ACommerce.Cache.Operations.Abstractions;

/// <summary>
/// خيارات إدخال للكاش. أحدهما أو كلاهما يمكن تحديده.
/// إن لم يُحدَّد أيّ شيء، الإدخال بلا انتهاء (حتى Eviction LRU من المزوّد).
/// </summary>
public sealed class CacheEntryOptions
{
    /// <summary>
    /// مهلة مطلقة من لحظة الإدخال. بعدها يُحذف الإدخال يقيناً.
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; init; }

    /// <summary>
    /// مهلة إنزلاقيّة: تتجدّد كلّما قُرأ المفتاح. مفيدة لـ session-like data.
    /// مزوّد Redis يطبّقها عبر EXPIRE في كلّ GET (overhead بسيط).
    /// </summary>
    public TimeSpan? SlidingExpiration { get; init; }

    public static CacheEntryOptions Absolute(TimeSpan ttl) => new() { AbsoluteExpiration = ttl };
    public static CacheEntryOptions Sliding(TimeSpan ttl)  => new() { SlidingExpiration  = ttl };
}
