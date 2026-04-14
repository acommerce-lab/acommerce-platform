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
window.acViewport = (() => {
    let mq = null, dotnet = null;
    const fire = () => {
        if (!dotnet || !mq) return;
        dotnet.invokeMethodAsync('OnChange', mq.matches, window.innerWidth);
    };
    return {
        register: (dotnetRef, breakpointPx) => {
            dotnet = dotnetRef;
            mq = window.matchMedia(`(max-width: ${breakpointPx}px)`);
            mq.addEventListener('change', fire);
            window.addEventListener('resize', fire, { passive: true });
            fire();
        },
        unregister: () => {
            if (mq) mq.removeEventListener('change', fire);
            window.removeEventListener('resize', fire);
            dotnet = null; mq = null;
        }
    };
})();
