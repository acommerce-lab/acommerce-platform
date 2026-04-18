// Runtime toggles for theme (light/dark) and direction (rtl/ltr).
// Called from MainLayout whenever AppStore.OnChanged fires.
window.ashareUi = window.ashareUi || {
  apply: function (theme, lang, dir) {
    var html = document.documentElement;
    var body = document.body;
    if (!body) return;
    html.setAttribute('lang', lang);
    html.setAttribute('dir', dir);
    if (theme === 'dark') body.classList.add('ac-dark');
    else body.classList.remove('ac-dark');
  }
};
