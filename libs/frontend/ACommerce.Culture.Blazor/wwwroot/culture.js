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
