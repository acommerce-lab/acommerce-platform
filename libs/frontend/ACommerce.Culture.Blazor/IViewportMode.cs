namespace ACommerce.Culture.Blazor;

/// <summary>
/// The single global variable the user asked for — read by any widget that
/// wants to emit a viewport-specific class suffix from C# instead of relying
/// on CSS alone.  Populated by <see cref="ViewportProbe"/>.
///
/// Convention: append the suffix to the base class, either as a modifier
/// (<c>class="ac-card @Vp.Modifier("ac-card")"</c>) or by picking between
/// two pre-defined class strings.
/// </summary>
public interface IViewportMode
{
    /// <summary>"mobile" or "desktop".</summary>
    string Current { get; }
    /// <summary>true when current mode is mobile.</summary>
    bool IsMobile { get; }
    /// <summary>true when current mode is desktop.</summary>
    bool IsDesktop { get; }
    /// <summary>Raised when the mode changes (breakpoint crossed).</summary>
    event Action? Changed;

    /// <summary>
    /// Helper used inside Razor tags:
    /// <code>&lt;div class="ac-card @Vp.Modifier("ac-card")"&gt;</code>
    /// yields <c>"ac-card ac-card--mobile"</c> or <c>"ac-card--desktop"</c>.
    /// </summary>
    string Modifier(string baseClass) => $"{baseClass}--{Current}";

    /// <summary>Picks one of two strings depending on current mode.</summary>
    T Pick<T>(T mobile, T desktop) => IsMobile ? mobile : desktop;
}

/// <summary>Bridge that hands off to the existing <see cref="ViewportState"/>.</summary>
public sealed class ViewportModeAdapter : IViewportMode
{
    private readonly ViewportState _state;

    public ViewportModeAdapter(ViewportState state)
    {
        _state = state;
        _state.Changed += () => Changed?.Invoke();
    }

    public string Current => _state.IsMobile ? "mobile" : "desktop";
    public bool IsMobile => _state.IsMobile;
    public bool IsDesktop => !_state.IsMobile;
    public event Action? Changed;
}
