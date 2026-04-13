// Root-attribute interop — writes data-theme / dir / lang to <html>.
// The root <html> element lives outside the interactive Blazor tree so
// Blazor can't update its attributes directly. MainLayout calls these
// helpers from OnAfterRenderAsync whenever UiPreferences fires a change.
window.acTheme = {
    set: function (theme) {
        if (theme !== 'light' && theme !== 'dark') return;
        document.documentElement.setAttribute('data-theme', theme);
    }
};

window.acLang = {
    set: function (lang) {
        if (lang !== 'ar' && lang !== 'en') return;
        document.documentElement.setAttribute('lang', lang);
        document.documentElement.setAttribute('dir', lang === 'ar' ? 'rtl' : 'ltr');
    }
};
