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
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.id = 'ac-install-btn';
    btn.setAttribute('aria-label', 'تثبيت تطبيق إيجار');
    btn.innerHTML = '<span style="font-size:18px;line-height:1;">⤓</span>'
                  + '<span>تثبيت التطبيق</span>';
    Object.assign(btn.style, {
      position: 'fixed',
      insetInlineStart: '16px',
      bottom: '16px',
      zIndex: '9999',
      display: 'inline-flex',
      alignItems: 'center',
      gap: '8px',
      padding: '10px 18px',
      borderRadius: '999px',
      border: 'none',
      background: '#1d4ed8',
      color: '#ffffff',
      fontFamily: 'Cairo, sans-serif',
      fontSize: '14px',
      fontWeight: '600',
      boxShadow: '0 6px 20px rgba(29,78,216,0.35)',
      cursor: 'pointer'
    });
    return btn;
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
    const btn = buildButton();
    document.body.appendChild(btn);
    btn.addEventListener('click', async () => {
      if (deferredPrompt) {
        deferredPrompt.prompt();
        const { outcome } = await deferredPrompt.userChoice;
        deferredPrompt = null;
        if (outcome === 'accepted') btn.remove();
      } else {
        showInstructions();
      }
    });
    window.__acInstallBtn = btn;
  });

  // ── Capture beforeinstallprompt to enable native flow ────────────────
  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;
  });

  window.addEventListener('appinstalled', () => {
    if (window.__acInstallBtn) window.__acInstallBtn.remove();
    deferredPrompt = null;
  });
})();
