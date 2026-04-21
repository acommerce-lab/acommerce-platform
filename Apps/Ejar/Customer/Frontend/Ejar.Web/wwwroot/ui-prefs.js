window.ejarUi = {
    apply: function (theme, lang, dir) {
        document.documentElement.setAttribute('data-theme', theme);
        document.documentElement.setAttribute('lang', lang);
        document.documentElement.setAttribute('dir', dir);
    }
};

window.ejarTz = {
    offset: function () { return new Date().getTimezoneOffset(); },
    name:   function () { return Intl.DateTimeFormat().resolvedOptions().timeZone ?? null; }
};
