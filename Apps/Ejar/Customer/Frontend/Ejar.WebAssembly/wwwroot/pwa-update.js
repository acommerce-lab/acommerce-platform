// pwa-update.js — يَكشِف نَسخَة جَديدَة مَن service-worker في waiting state
// + يَرسُم شَريط تَحديث مَرئي لِلمُستَخدِم في DOM (بَدون Blazor JSInterop،
// يَعمَل فَوراً قَبل تَحميل WASM).
//
// السُلوك السابِق: SW يَستَدعي skipWaiting صَامِتاً ⇒ المُستَخدِم لا يَرى
// التَّحديث. الجَديد: نَسأَله "حَدِّث الآن؟ / لاحِقاً" قَبل skipWaiting.

(function () {
  if (!('serviceWorker' in navigator)) return;

  let waitingSw = null;
  let bannerEl  = null;

  // اِبنِ الـ banner مَرَّة واحِدَة عِندَ الحاجَة.
  function showBanner() {
    if (bannerEl) return;
    const html = `
      <div id="ejar-pwa-banner" style="
        position:fixed; top:0; left:0; right:0; z-index:99999;
        background:#2e7d6b; color:#fff; padding:10px 16px;
        display:flex; align-items:center; justify-content:space-between;
        gap:12px; font:14px/1.4 system-ui,Arial,sans-serif;
        box-shadow:0 2px 8px rgba(0,0,0,.15);">
        <span>تَوَفَّرَ تَحديث جَديد لِلتَطبيق</span>
        <div style="display:flex; gap:8px;">
          <button id="ejar-pwa-refresh" style="
            background:#fff; color:#2e7d6b; font-weight:600;
            padding:6px 14px; border:0; border-radius:6px; cursor:pointer;">تَحديث الآن</button>
          <button id="ejar-pwa-dismiss" style="
            background:transparent; color:#fff;
            padding:6px 14px; border:1px solid rgba(255,255,255,.4);
            border-radius:6px; cursor:pointer;">لاحِقاً</button>
        </div>
      </div>`;
    const tmpl = document.createElement('template');
    tmpl.innerHTML = html.trim();
    bannerEl = tmpl.content.firstElementChild;
    document.body.appendChild(bannerEl);

    bannerEl.querySelector('#ejar-pwa-refresh').addEventListener('click', applyUpdate);
    bannerEl.querySelector('#ejar-pwa-dismiss').addEventListener('click', dismissBanner);
  }

  function dismissBanner() {
    if (bannerEl && bannerEl.parentNode) bannerEl.parentNode.removeChild(bannerEl);
    bannerEl = null;
  }

  function applyUpdate() {
    if (waitingSw) waitingSw.postMessage('skipWaiting');
    // controllerchange listener يُعيد التَّحميل تِلقائيّاً
  }

  navigator.serviceWorker.ready.then(reg => {
    if (reg.waiting) { waitingSw = reg.waiting; showBanner(); }

    reg.addEventListener('updatefound', () => {
      const newSw = reg.installing;
      if (!newSw) return;
      newSw.addEventListener('statechange', () => {
        if (newSw.state === 'installed' && navigator.serviceWorker.controller) {
          waitingSw = newSw;
          showBanner();
        }
      });
    });
  }).catch(() => { /* بِلا SW = لا شَيء */ });

  let reloading = false;
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (reloading) return;
    reloading = true;
    window.location.reload();
  });
})();
