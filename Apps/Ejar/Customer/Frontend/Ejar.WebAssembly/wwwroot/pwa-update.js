// pwa-update.js — يَكشِف نَسخَة جَديدَة مَن service-worker في waiting state
// + يَرسُم شَريط تَحديث مَرئي لِلمُستَخدِم في DOM (بَدون Blazor JSInterop،
// يَعمَل فَوراً قَبل تَحميل WASM).
//
// السُلوك السابِق: SW يَستَدعي skipWaiting صَامِتاً ⇒ المُستَخدِم لا يَرى
// التَّحديث. الجَديد: نَسأَله "حَدِّث الآن؟ / لاحِقاً" قَبل skipWaiting.

// window.acVersionRefresh — دالّة عامّة آمنة CSP يَستَدعيها Blazor (عَبر
// IJSRuntime.InvokeVoidAsync) بَدَل eval. تُلغي تَسجيل SW + تَمسَح caches +
// تُعيد التَّحميل مع ?ac_v=<ts> لِكَسر CDN cache. تُسجَّل فَوراً قَبل
// تَحميل WASM فَتَكون مُتاحَة عِندَما يَنقُر المُستَخدِم زَرّ "تَحديث الآن"
// (سَواء في VersionPoll banner أو AcVersionBanner).
//
// تَعرِض overlay فَوريّ "جاري التَّحديث..." قَبل بَدء التَّنظيف لِأَنّ
// unregister + caches.delete يَستَغرِق ثَوانٍ — بِدونه المُستَخدِم يَرى
// "لا شَيء" بَعد النَّقر فَيَنقُر مَرّة أُخرى أو يَظُنّ أَنّ الزَّرّ مَكسور
// (وَهذا تَقريباً ما حَصَل عِندَ الناشِر).
window.acVersionRefresh = async function () {
  showRefreshOverlay();
  try {
    if ('serviceWorker' in navigator) {
      const regs = await navigator.serviceWorker.getRegistrations();
      await Promise.all(regs.map(r => r.unregister().catch(() => {})));
    }
    if ('caches' in window) {
      const keys = await caches.keys();
      await Promise.all(keys.map(k => caches.delete(k).catch(() => {})));
    }
  } catch (e) { console.warn('[acVersionRefresh] cleanup failed', e); }
  try {
    const u = new URL(location.href);
    u.searchParams.set('ac_v', Date.now().toString());
    location.replace(u.toString());
  } catch {
    location.reload();
  }
};

function showRefreshOverlay() {
  if (document.getElementById('ac-refresh-overlay')) return;
  const div = document.createElement('div');
  div.id = 'ac-refresh-overlay';
  div.style.cssText = [
    'position:fixed', 'inset:0',
    'background:#1d4ed8', 'color:#fff',
    'display:flex', 'align-items:center', 'justify-content:center',
    'flex-direction:column', 'gap:16px',
    'z-index:2147483647',
    'font-family:Cairo,system-ui,sans-serif', 'font-size:18px',
    'direction:rtl'
  ].join(';');
  div.innerHTML = `
    <div style="font-size:22px;font-weight:700">جاري تَحديث إيجار…</div>
    <div style="font-size:14px;opacity:0.85">لَحظَة مِن فَضلك، لا تُغلِق التَّطبيق</div>
    <div style="margin-top:8px;width:48px;height:48px;border:4px solid rgba(255,255,255,0.3);
                border-top-color:#fff;border-radius:50%;animation:ac-refresh-spin 1s linear infinite"></div>
    <style>@keyframes ac-refresh-spin{to{transform:rotate(360deg)}}</style>`;
  (document.body || document.documentElement).appendChild(div);
}

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
    // overlay فَوريّ — controllerchange قَد يَستَغرِق ثَوانٍ، بِدون رَدّ
    // فِعل بَصَريّ المُستَخدِم يَظُنّ أَنّ الزَّرّ مَكسور.
    showRefreshOverlay();
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
