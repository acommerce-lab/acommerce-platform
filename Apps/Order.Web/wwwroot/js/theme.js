// Theme interop — writes data-theme to <html> when Blazor toggles theme.
// <html data-theme> is rendered server-side by App.razor on first request
// but the interactive tree can't update the root element directly, so we
// push the new value via JS whenever MainLayout sees Ui.Theme change.
window.acTheme = {
    set: function (theme) {
        if (theme !== 'light' && theme !== 'dark') return;
        document.documentElement.setAttribute('data-theme', theme);
    }
};
