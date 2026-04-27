// زر «تثبيت التطبيق» مرئي دائماً (إلا لو التطبيق يعمل بالفعل في وضع
// مثبَّت أو على متصفّح لا يدعم PWA). يتعامل مع ثلاث حالات:
//
//   ① Chrome/Edge أطلقا beforeinstallprompt — نخزّن الحدث ونطلقه عند الضغط
//   ② Chrome/Edge لم يطلقاه بعد — نعرض تعليمات «انقر على أيقونة التثبيت ⤓
//      في شريط العنوان» (يحدث حين معايير PWA لم يقتنع بها المتصفّح بعد)
//   ③ Safari iOS لا API — نعرض «شارك → إضافة إلى الشاشة الرئيسية»
//
// الزرّ يختفي تلقائياً بعد التثبيت (appinstalled أو display-mode: standalone).

(function () {
  let deferredPrompt = null;

  const ua = navigator.userAgent;
  const isIOS = /iPad|iPhone|iPod/.test(ua) && !window.MSStream;
  const isAndroid = /Android/.test(ua);
  const isChromiumBased = /Chrome|CriOS|Edg|EdgA/.test(ua);
  const isSafari = /Safari/.test(ua) && !isChromiumBased;

  const isStandalone =
    window.matchMedia('(display-mode: standalone)').matches ||
    window.navigator.standalone === true;

  if (isStandalone) return;

  const PWA_NOT_SUPPORTED = !('serviceWorker' in navigator);
  if (PWA_NOT_SUPPORTED) return;

  function buildButton() {
    const wrap = document.createElement('div');
    wrap.id = 'ac-install-wrap';
    // !important لتعطيل أيّ CSS من الـ RCL أو الـ widgets قد يبطل
    // الموقع. fixed + top يضع الزر فوق كل المحتوى وبعيداً عن أيّ
    // قائمة سفلية. z-index عالٍ جداً ليبقى ظاهراً فوق header الـ app.
    wrap.style.cssText = [
      'position:fixed!important',
      'inset-inline-start:12px!important',
      'top:calc(12px + env(safe-area-inset-top,0px))!important',
      'bottom:auto!important',
      'inset-inline-end:auto!important',
      'z-index:2147483646!important',
      'display:inline-flex!important',
      'align-items:center!important',
      'gap:4px!important',
      'background:#1d4ed8!important',
      'color:#ffffff!important',
      'border-radius:999px!important',
      'box-shadow:0 6px 20px rgba(29,78,216,0.35)!important',
      'font-family:Cairo,sans-serif!important',
      'font-size:13px!important',
      'font-weight:600!important',
      'pointer-events:auto!important'
    ].join(';');

    const btn = document.createElement('button');
    btn.type = 'button';
    btn.id = 'ac-install-btn';
    btn.setAttribute('aria-label', 'تثبيت تطبيق إيجار');
    btn.innerHTML = '<span style="font-size:16px;line-height:1;">⤓</span>'
                  + '<span>تثبيت التطبيق</span>';
    Object.assign(btn.style, {
      display: 'inline-flex', alignItems: 'center', gap: '6px',
      padding: '8px 14px', border: 'none', background: 'transparent',
      color: 'inherit', font: 'inherit', cursor: 'pointer',
      borderRadius: '999px 0 0 999px'
    });

    // زر إغلاق صغير — يخفي البانر لـ 7 أيام في localStorage
    const close = document.createElement('button');
    close.type = 'button';
    close.id = 'ac-install-close-btn';
    close.setAttribute('aria-label', 'إخفاء');
    close.textContent = '×';
    Object.assign(close.style, {
      padding: '4px 10px 6px', border: 'none', background: 'transparent',
      color: 'rgba(255,255,255,0.85)', font: 'inherit', cursor: 'pointer',
      fontSize: '18px', lineHeight: '1', borderRadius: '0 999px 999px 0'
    });
    close.addEventListener('click', (e) => {
      e.stopPropagation();
      try { localStorage.setItem('ac.install.dismissed', String(Date.now())); } catch { }
      wrap.remove();
    });

    wrap.appendChild(btn);
    wrap.appendChild(close);
    wrap.__btn = btn;
    return wrap;
  }

  function recentlyDismissed() {
    try {
      const t = parseInt(localStorage.getItem('ac.install.dismissed') || '0', 10);
      const sevenDays = 7 * 24 * 60 * 60 * 1000;
      return t > 0 && (Date.now() - t) < sevenDays;
    } catch { return false; }
  }

  function showInstructions() {
    const dlg = document.createElement('div');
    dlg.id = 'ac-install-dialog';
    Object.assign(dlg.style, {
      position: 'fixed',
      inset: '0',
      background: 'rgba(15,23,42,0.55)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: '10000',
      padding: '20px'
    });

    let body;
    if (isIOS) {
      body = `<p style="margin:0 0 12px"><strong>على iPhone / iPad:</strong></p>
              <ol style="margin:0;padding-inline-start:18px;line-height:2">
                <li>اضغط زرّ المشاركة <span style="font-size:18px">⬆︎</span> في Safari</li>
                <li>اختر «إضافة إلى الشاشة الرئيسية»</li>
                <li>اضغط «إضافة» ليظهر إيجار كتطبيق منفصل</li>
              </ol>`;
    } else if (isAndroid) {
      body = `<p style="margin:0 0 12px"><strong>على Android:</strong></p>
              <ol style="margin:0;padding-inline-start:18px;line-height:2">
                <li>اضغط ⋮ في أعلى Chrome</li>
                <li>اختر «تثبيت التطبيق» أو «إضافة إلى الشاشة الرئيسية»</li>
              </ol>
              <p style="margin:12px 0 0;font-size:13px;color:#64748b">
                إن لم يظهر الخيار، يحتاج المتصفّح بعض الثواني للاعتراف بالتطبيق.
                أعد التحميل بعد قليل.</p>`;
    } else if (isChromiumBased) {
      body = `<p style="margin:0 0 12px"><strong>على Chrome/Edge:</strong></p>
              <ol style="margin:0;padding-inline-start:18px;line-height:2">
                <li>ابحث عن أيقونة التثبيت <span style="font-size:18px">⊕</span> في شريط العنوان</li>
                <li>أو افتح القائمة (⋮) واختر «تثبيت إيجار…»</li>
              </ol>
              <p style="margin:12px 0 0;font-size:13px;color:#64748b">
                إن لم تظهر الأيقونة، تصفّح الصفحة لثوانٍ ثم أعد المحاولة —
                المتصفّح يحتاج بعض التفاعل قبل عرض خيار التثبيت.</p>`;
    } else {
      body = `<p>متصفّحك الحاليّ لا يدعم تثبيت تطبيقات الويب التقدّمية. جرّب
              Chrome أو Edge أو Safari (iOS).</p>`;
    }

    dlg.innerHTML = `
      <div style="background:#fff;border-radius:16px;padding:24px;max-width:440px;
                  font-family:Cairo,sans-serif;color:#0f172a;direction:rtl;
                  box-shadow:0 20px 50px rgba(0,0,0,0.3)">
        <h3 style="margin:0 0 12px;font-size:18px">تثبيت إيجار على جهازك</h3>
        ${body}
        <button id="ac-install-close" style="margin-top:16px;padding:8px 16px;
                  border:none;border-radius:8px;background:#1d4ed8;color:#fff;
                  font-family:inherit;font-size:14px;cursor:pointer">حسناً</button>
      </div>`;
    document.body.appendChild(dlg);
    dlg.addEventListener('click', e => {
      if (e.target === dlg || e.target.id === 'ac-install-close') dlg.remove();
    });
  }

  // ── DOM-ready: place the button ───────────────────────────────────────
  function ready(fn) {
    if (document.readyState !== 'loading') fn();
    else document.addEventListener('DOMContentLoaded', fn);
  }
  ready(() => {
    if (recentlyDismissed()) return;
    const wrap = buildButton();
    const btn = wrap.__btn;
    document.body.appendChild(wrap);
    btn.addEventListener('click', async () => {
      if (deferredPrompt) {
        deferredPrompt.prompt();
        const { outcome } = await deferredPrompt.userChoice;
        deferredPrompt = null;
        if (outcome === 'accepted') wrap.remove();
      } else {
        showInstructions();
      }
    });
    window.__acInstallWrap = wrap;
  });

  // ── Capture beforeinstallprompt to enable native flow ────────────────
  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;
  });

  window.addEventListener('appinstalled', () => {
    if (window.__acInstallWrap) window.__acInstallWrap.remove();
    deferredPrompt = null;
  });
})();
