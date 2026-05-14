// JS interop module لِـ ITimezoneProvider — يُنادى مِن .NET عَبر InvokeAsync.
window.acTz = {
    offset: function () { return new Date().getTimezoneOffset(); },
    name:   function () { return Intl.DateTimeFormat().resolvedOptions().timeZone ?? null; }
};
