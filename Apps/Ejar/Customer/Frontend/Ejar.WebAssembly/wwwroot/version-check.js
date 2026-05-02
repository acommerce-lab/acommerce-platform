// آلية تحديث مضمونة للـ PWA — تعمل بغض النظر عن حالة cache المتصفّح
// أو الـ service worker القديم. تُحَلّ مشكلة "نشرت تحديثاً لكن الجهاز
// ما زال يعرض القديم".
//
// كيف تعمل:
//   1. عند كل بدء (قبل أن يقلع Blazor) نجلب /version.json بـ no-store —
//      هذا الـ fetch يتجاوز كل layers الـ cache.
//   2. نقارن مع آخر نسخة محفوظة في localStorage('ac.pwa.version').
//   3. لو اختلفت أو لم تكن محفوظة:
//        a. نُلغي تسجيل كل service worker للنطاق (SWs قديمة قد تخدم shell قديم).
//        b. نحذف كل caches (يقتل أيّ shell مخزّن).
//        c. نحدّث القيمة المحفوظة محلياً.
//        d. نُعيد التحميل بـ ?ac_v=<new_version> ليُسقط أيّ HTTP cache على
//           الـ proxy/CDN ويُعطي نسخة جديدة من index.html نفسه.
//   4. لو متطابقة، لا نعمل شيء — تكلفة خفيفة جداً (~200 بايت).
//
// كيف ترفع نسخة عند كل نشر:
//   - عدّل version في wwwroot/version.json.
//   - بنبديل الـ VERSION داخل service-worker.js (عدد ثابت لكنه مختلف).
//   - ادفع وانشر — كل عميل سيتحدث تلقائياً عند فتح التطبيق المرّة القادمة.

(async function () {
  const STORAGE_KEY = 'ac.pwa.version';

  let serverVersion;
  try {
    const r = await fetch('version.json', { cache: 'no-store' });
    if (!r.ok) throw new Error('version.json HTTP ' + r.status);
    const j = await r.json();
    serverVersion = j.version;
  } catch (e) {
    // فشل القراءة — مثلاً offline أو الخدمة معطّلة. لا نتدخّل، نترك Blazor
    // يحاول. (في النهاية لو offline، لن يتوقّع التحديث أصلاً.)
    console.warn('[Ejar update] لا يمكن قراءة version.json:', e?.message || e);
    return;
  }

  const localVersion = (() => {
    try { return localStorage.getItem(STORAGE_KEY); }
    catch { return null; }
  })();

  // اقرأ نسخة appsettings.json أيضاً — هذه النسخة "المُجمَّعة" مع البناء.
  // لو كانت أقدم من server's version.json، فالـ shell الذي يحمله المتصفّح
  // قديم (cache HTTP أو SW) ويجب فرض إعادة التحميل حتى لو localStorage
  // فاضي (أوّل زيارة على جهاز كان يحمل نسخة قديمة في cache المتصفّح).
  let bundledVersion = null;
  try {
    const r2 = await fetch('appsettings.json', { cache: 'no-store' });
    if (r2.ok) {
      const j2 = await r2.json();
      bundledVersion = j2 && j2.App && j2.App.Version;
    }
  } catch { /* غير قاتل */ }

  // bundledVersion قد يكون null لو appsettings.json ما يحتوي App.Version
  // (إعداد قديم، أو frontend بدون الحقل أصلاً). في هذه الحالة لا نستطيع
  // الحكم على cache المتصفّح مقابل النشر، فنُسقط المقارنة الثانية بدل
  // فرض إعادة تحميل أبديّة (lesson learned من حلقة 2026.05.02 — كان
  // App.Version في appsettings.json متخلّفاً عن version.json فيُعتبر
  // bundle "قديم" دائماً ويُعيد التحميل بلا نهاية).
  const bundleOK = (bundledVersion === null) || (bundledVersion === serverVersion);

  if (localVersion === serverVersion && bundleOK) {
    console.info('[Ejar update] نسخة محدّثة:', serverVersion);
    return;
  }

  // أوّل زيارة + bundle مطابق (أو غير متاح) → سجّل واخرج (حالة نظيفة).
  if (localVersion === null && bundleOK) {
    try { localStorage.setItem(STORAGE_KEY, serverVersion); } catch { }
    console.info('[Ejar update] تسجيل أولي:', serverVersion);
    return;
  }

  // حماية من حلقة إعادة التحميل: لو فُرض تحديث خلال آخر ٢٠ ثانية، لا
  // نُكرّر — بدلاً من ذلك نُسجّل النسخة محلياً ونخرج. حلقة لا نهائيّة
  // ضارّة جدّاً (بطّاريّة + بيانات + تجربة) فالأفضل ترك الـ shell الحاليّ
  // يعمل حتّى يحلّ المشكلة دفع/نشر لاحق.
  try {
    const last = parseInt(sessionStorage.getItem('ac.pwa.last_force') || '0', 10);
    const now  = Date.now();
    if (last && (now - last) < 20000) {
      console.warn('[Ejar update] منع حلقة إعادة تحميل — تخطّي force reload');
      try { localStorage.setItem(STORAGE_KEY, serverVersion); } catch { }
      return;
    }
    sessionStorage.setItem('ac.pwa.last_force', String(now));
  } catch { /* sessionStorage معطّل — استمر */ }

  // إمّا localStorage قديم، أو bundle المخزَّن في cache قديم → فرض تحديث.
  console.warn('[Ejar update] تحديث:',
    'local=' + localVersion, 'bundled=' + bundledVersion, '→ server=' + serverVersion);
  showUpdatingBanner(serverVersion);

  await wipeStaleData();

  try { localStorage.setItem(STORAGE_KEY, serverVersion); } catch { }

  // أعد التحميل بـ query لكسر cache الوسيط — المتصفّح + CDN + proxy
  const url = new URL(location.href);
  url.searchParams.set('ac_v', serverVersion);
  location.replace(url.toString());

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
      <style>@keyframes ac-spin{to{transform:rotate(360deg)}}</style>`;
    if (document.body) document.body.appendChild(div);
    else document.addEventListener('DOMContentLoaded', () => document.body.appendChild(div));
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
    // نُبقي ما يخصّ المستخدم (token الدخول، التفضيلات) — فقط نمسح
    // مفاتيح shell الداخلية التي قد تكون قديمة.
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
