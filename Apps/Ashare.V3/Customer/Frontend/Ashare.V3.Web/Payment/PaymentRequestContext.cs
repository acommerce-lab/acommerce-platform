namespace Ashare.V3.Web.Payment;

/// <summary>
/// سياق reference دَفع لِلطَلَب الحالي. <see cref="AsyncLocal{T}"/> يَضمَن
/// أَنّ كُلّ async chain لَه نُسخَته الخاصَّة — لا تَزاحُم بَين tabs أَو
/// عَمَلِيّات مُتَوازِيَة في نَفس الـ Blazor circuit.
///
/// <para>الاستِخدام النَّموذَجي:</para>
/// <code>
/// using (PaymentContext.Use("mock_abc"))
///     await Listings.CreateAsync(payload);
/// </code>
/// </summary>
public sealed class PaymentRequestContext
{
    private static readonly AsyncLocal<string?> _reference = new();

    public string? Reference => _reference.Value;

    /// <summary>
    /// يَضَع reference لِنِطاق الـ async chain الحالي ويُرجِع <see cref="IDisposable"/>
    /// يُنَظِّفها عِند Dispose. مَضمونَة حَتّى مَع exception (<c>using</c>).
    /// </summary>
    public IDisposable Use(string reference)
    {
        var previous = _reference.Value;
        _reference.Value = reference;
        return new Restore(() => _reference.Value = previous);
    }

    private sealed class Restore : IDisposable
    {
        private readonly Action _onDispose;
        private int _disposed;
        public Restore(Action onDispose) => _onDispose = onDispose;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) _onDispose();
        }
    }
}
