// Exposes the browser's timezone to the Blazor server via a small helper.
window.acCulture = {
    getTimeZone: () => Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
    getLanguage: () => (navigator.language || "ar").split("-")[0],
    // ISO-ish guess: if the browser locale extension mentions "nu-arab" or
    // "nu-arabext" we expose the corresponding numeral system, else "latin".
    getNumeralSystem: () => {
        const opts = Intl.DateTimeFormat().resolvedOptions();
        if (opts.numberingSystem === "arab") return "arabic-indic";
        if (opts.numberingSystem === "arabext") return "persian";
        return "latin";
    }
};

// Viewport probe — reports current breakpoint + emits on every crossing.
// Also sets a GLOBAL data attribute `<html data-ac-mode="mobile|desktop">`
// so CSS rules can target either mode without touching any widget's markup.
window.acViewport = (() => {
    let mq = null, dotnet = null;
    const setGlobal = isMobile => {
        document.documentElement.dataset.acMode = isMobile ? 'mobile' : 'desktop';
    };
    const fire = () => {
        if (mq) setGlobal(mq.matches);
        if (!dotnet || !mq) return;
        dotnet.invokeMethodAsync('OnChange', mq.matches, window.innerWidth);
    };
    // Set the global attribute on page load even before any C# service
    // registers — CSS-only usage doesn't need Blazor interop.
    const bootstrap = (breakpointPx = 768) => {
        if (mq) return;
        mq = window.matchMedia(`(max-width: ${breakpointPx}px)`);
        mq.addEventListener('change', fire);
        window.addEventListener('resize', fire, { passive: true });
        setGlobal(mq.matches);
    };
    // Auto-bootstrap so CSS `[data-ac-mode]` works from first paint.
    document.addEventListener('DOMContentLoaded', () => bootstrap());
    if (document.readyState !== 'loading') bootstrap();
    return {
        register: (dotnetRef, breakpointPx) => {
            dotnet = dotnetRef;
            bootstrap(breakpointPx);
            fire();
        },
        unregister: () => {
            dotnet = null;
        },
        currentMode: () => (mq && mq.matches) ? 'mobile' : 'desktop'
    };
})();
