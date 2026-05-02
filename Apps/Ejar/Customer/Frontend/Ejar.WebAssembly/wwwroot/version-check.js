// آلية تحديث مأمونة من الحلقات للـ PWA — نسخة مُعاد تصميمها بعد حادثة
// 2026.05.02 (حلقة لانهائيّة بسبب مزامنة ملفّين بشريّاً).
//
// مبدأ التصميم الجديد:
//   1. مصدر واحد للحقيقة: version.json. لا مقارنة مع appsettings.json.
//   2. حماية الحلقة قبل أيّ شيء: لو حصل reload خلال آخر ٣٠ ثانية، نخرج
//      تماماً (بدون حتى fetch). متصفّح عالق يهدأ تلقائياً بعد reload واحد.
//   3. أوّل زيارة (localStorage فاضي) لا تُعيد التحميل — تُسجِّل وتمضي.
//      السبب: المستخدم وصل للموقع من جديد، الـ shell هو أحدث ما يعرفه
//      الخادم (إلّا CDN قديم وذلك مشكلة CDN لا shell).
//   4. localStorage يُحفَظ <i>قبل</i> reload لا بعده — فلو وقع خلل بعد
//      reload، الجلسة التالية ترى المطابقة فلا تكرّر.
//   5. تأخير ٢ ثانية قبل reload + بانر قابل للإلغاء — يَمنح المستخدم
//      فرصة قراءة الرسالة + إيقاف الحلقة لو ساءت.
//
// كيفية رفع نسخة بعد اليوم:
//   - فقط: حدّث wwwroot/version.json (version + released_at).
//   - اختياريّ: ارفع App.Version في appsettings.json (يُرسَل كـ
//     X-App-Version header)، لكنّه لا يؤثّر على آلية التحديث.

(async function () {
  const STORAGE_KEY    = 'ac.pwa.version';
  const LOOP_GUARD_KEY = 'ac.pwa.last_force';
  const LOOP_WINDOW_MS = 30000;
  const RELOAD_DELAY_MS = 2000;

  // ① حماية الحلقة — قبل أيّ fetch، قبل أيّ DOM. الأكثر أهميّة في الملفّ.
  try {
    const last = parseInt(sessionStorage.getItem(LOOP_GUARD_KEY) || '0', 10);
    if (last && (Date.now() - last) < LOOP_WINDOW_MS) {
      console.warn('[Ejar update] حماية حلقة — تخطّي فحص التحديث هذه الجلسة');
      return;
    }
  } catch { /* sessionStorage معطّل — استمر بحذر */ }

  // ② اقرأ version.json — مصدر الحقيقة الوحيد.
  let serverVersion;
  try {
    const r = await fetch('version.json', { cache: 'no-store' });
    if (!r.ok) { console.warn('[Ejar update] version.json HTTP', r.status); return; }
    const j = await r.json();
    serverVersion = j && j.version;
    if (!serverVersion) return;
  } catch (e) {
    console.warn('[Ejar update] لا يمكن قراءة version.json:', e?.message || e);
    return;
  }

  const localVersion = (() => {
    try { return localStorage.getItem(STORAGE_KEY); }
    catch { return null; }
  })();

  // ③ أوّل زيارة → سجّل واخرج بدون reload.
  if (localVersion === null) {
    try { localStorage.setItem(STORAGE_KEY, serverVersion); } catch {}
    console.info('[Ejar update] تسجيل أوّل:', serverVersion);
    return;
  }

  // ④ متطابقة → لا شيء.
  if (localVersion === serverVersion) {
    console.info('[Ejar update] محدّث:', serverVersion);
    return;
  }

  // ⑤ مختلفة → جدوِل reload واحدة بعد ٢ ثانية.
  console.warn('[Ejar update] تحديث:', localVersion, '→', serverVersion);

  // اِحفِظ <i>قبل</i> reload + اضبط حماية الحلقة. لو reload أتى لأيّ سبب
  // سيقرأ localStorage = serverVersion ويخرج فوراً (الفرع ④).
  try { localStorage.setItem(STORAGE_KEY, serverVersion); } catch {}
  try { sessionStorage.setItem(LOOP_GUARD_KEY, String(Date.now())); } catch {}

  showUpdatingBanner(serverVersion);
  setTimeout(async () => {
    await wipeStaleData();
    const url = new URL(location.href);
    url.searchParams.set('ac_v', serverVersion);
    location.replace(url.toString());
  }, RELOAD_DELAY_MS);

  // ─── helpers ─────────────────────────────────────────────────────

  function showUpdatingBanner(v) {
    const div = document.createElement('div');
    div.id = 'ac-updating';
    div.style.cssText = [
      'position:fixed!important', 'inset:0!important',
      'background:#1d4ed8!important', 'color:#fff!important',
      'display:flex!important', 'align-items:center!important',
      'justify-content:center!important', 'flex-direction:column!important',
      'z-index:2147483647!important', 'font-family:Cairo,sans-serif!important',
      'font-size:18px!important', 'gap:16px!important', 'direction:rtl!important'
    ].join(';');
    div.innerHTML = `
      <div style="font-size:22px;font-weight:700">جاري تحديث إيجار…</div>
      <div style="font-size:14px;opacity:0.85">النسخة ${escapeHtml(v)}</div>
      <div style="margin-top:8px;width:48px;height:48px;border:4px solid rgba(255,255,255,0.3);
                  border-top-color:#fff;border-radius:50%;animation:ac-spin 1s linear infinite"></div>
      <button id="ac-skip-update" type="button"
              style="margin-top:18px;background:rgba(255,255,255,0.18);border:1px solid rgba(255,255,255,0.4);
                     color:#fff;padding:8px 18px;border-radius:8px;font-family:inherit;font-size:14px;cursor:pointer">
        تخطّي التحديث الآن</button>
      <style>@keyframes ac-spin{to{transform:rotate(360deg)}}</style>`;
    const attach = () => {
      document.body.appendChild(div);
      const skip = div.querySelector('#ac-skip-update');
      if (skip) skip.addEventListener('click', () => {
        // المستخدم يطلب الاستمرار بـ shell الحاليّ — احترم ذلك ولا تُعِد
        // التحميل في هذه الجلسة. الحلقة (لو طرأت) تتوقّف هنا تماماً.
        try { sessionStorage.setItem(LOOP_GUARD_KEY, String(Date.now())); } catch {}
        div.remove();
      });
    };
    if (document.body) attach();
    else document.addEventListener('DOMContentLoaded', attach);
  }

  async function wipeStaleData() {
    if ('serviceWorker' in navigator) {
      try {
        const regs = await navigator.serviceWorker.getRegistrations();
        await Promise.all(regs.map(r => r.unregister().catch(() => false)));
      } catch (e) { console.warn('[Ejar update] SW unregister failed:', e); }
    }
    if ('caches' in window) {
      try {
        const keys = await caches.keys();
        await Promise.all(keys.map(k => caches.delete(k).catch(() => false)));
      } catch (e) { console.warn('[Ejar update] caches delete failed:', e); }
    }
    // نُبقي token + lokalStorage الحسّاس — نمسح فقط مفاتيح shell الداخليّة.
    try {
      const keep = ['ac.auth.token', 'ac.pwa.version', 'ac.install.dismissed'];
      const toDel = [];
      for (let i = 0; i < localStorage.length; i++) {
        const k = localStorage.key(i);
        if (k && !keep.includes(k) && k.startsWith('ac.')) toDel.push(k);
      }
      toDel.forEach(k => localStorage.removeItem(k));
    } catch { }
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({
      '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'
    }[c]));
  }
})();
