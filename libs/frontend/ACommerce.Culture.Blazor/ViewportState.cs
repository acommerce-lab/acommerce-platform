using Microsoft.JSInterop;

namespace ACommerce.Culture.Blazor;

/// <summary>
/// Per-circuit viewport state.  CSS alone handles show/hide via the
/// AcViewportSwitch widget, but some components want to know whether
/// they're mobile-sized so they can fetch less data, load fewer images,
/// skip a hover-only tooltip, etc.  This service lets them ask.
///
/// Populated by <see cref="ViewportProbe"/> on first render AND on every
/// breakpoint crossing (the JS helper uses window.matchMedia and pings
/// back via a DotNetObjectReference).
/// </summary>
public sealed class ViewportState
{
    /// <summary>true when viewport width ≤ the configured breakpoint (default 768px).</summary>
    public bool IsMobile { get; private set; }

    /// <summary>current width in CSS pixels, 0 if unknown.</summary>
    public int Width { get; private set; }

    public event Action? Changed;

    internal void Apply(bool isMobile, int width)
    {
        var changed = IsMobile != isMobile || Width != width;
        IsMobile = isMobile;
        Width = width;
        if (changed) Changed?.Invoke();
    }
}

/// <summary>
/// Wires a window.matchMedia listener so the ViewportState reflects the
/// browser's current size and every breakpoint crossing.
/// </summary>
public sealed class ViewportProbe : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly ViewportState _state;
    private DotNetObjectReference<ViewportProbe>? _self;
    public bool Initialised { get; private set; }

    public ViewportProbe(IJSRuntime js, ViewportState state)
    { _js = js; _state = state; }

    public async Task InitAsync(int breakpointPx = 768, CancellationToken ct = default)
    {
        if (Initialised) return;
        try
        {
            _self = DotNetObjectReference.Create(this);
            await _js.InvokeVoidAsync("acViewport.register", ct, _self, breakpointPx);
            Initialised = true;
        }
        catch { /* JS not ready during pre-render, etc. */ }
    }

    [JSInvokable]
    public void OnChange(bool isMobile, int width) => _state.Apply(isMobile, width);

    public async ValueTask DisposeAsync()
    {
        try { if (Initialised) await _js.InvokeVoidAsync("acViewport.unregister"); } catch { }
        _self?.Dispose();
    }
}
