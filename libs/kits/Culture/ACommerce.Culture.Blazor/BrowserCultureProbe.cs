using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using Microsoft.JSInterop;

namespace ACommerce.Culture.Blazor;

/// <summary>
/// Blazor-Server side helper.  Call `InitAsync` once after first render
/// to populate the per-circuit <see cref="MutableCultureContext"/> from
/// `Intl.DateTimeFormat().resolvedOptions()` in the browser.
/// </summary>
public sealed class BrowserCultureProbe
{
    private readonly IJSRuntime _js;
    private readonly MutableCultureContext _ctx;
    public bool Initialised { get; private set; }

    public BrowserCultureProbe(IJSRuntime js, MutableCultureContext ctx)
    { _js = js; _ctx = ctx; }

    public async Task InitAsync(CancellationToken ct = default)
    {
        if (Initialised) return;
        try
        {
            var tz        = await _js.InvokeAsync<string>("acCulture.getTimeZone", ct);
            var lang      = await _js.InvokeAsync<string>("acCulture.getLanguage", ct);
            var numerals  = await _js.InvokeAsync<string>("acCulture.getNumeralSystem", ct);
            _ctx.Set(tz: tz, lang: lang, numerals: numerals);
            Initialised = true;
        }
        catch
        {
            // JS not ready during pre-render, etc. — leave defaults.
        }
    }
}
