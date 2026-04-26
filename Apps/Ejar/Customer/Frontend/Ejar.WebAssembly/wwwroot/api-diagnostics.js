// تشخيص في وقت التشغيل لاتصال PWA بالخدمة الخلفية. يستهدف الفخّ الشائع
// عند نشر PWA على HTTPS مع backend على HTTP — المتصفّح يحجب fetch
// صامتاً ويعطي «TypeError: Failed to fetch» بدون أيّ تفصيل.
//
// آليّة العمل:
//   1. اقرأ EjarApi.BaseUrl من appsettings.json (نفس مصدر Program.cs).
//   2. استنتج ثلاث حالات قبل أن يحاول التطبيق:
//        • mixed-content: الصفحة https والـ backend http  → سيفشل fetch
//        • origin-mismatch: backend على نطاق مختلف   → preflight CORS مطلوب
//        • baseurl-default: لا تزال القيمة "https://api.ejar.ye" → نسيت تعديلها
//   3. اعمل fetch تجريبيّ على /healthz لاختبار الاتصال الحيّ.
//   4. لو فشل أيّ شيء، اعرض banner مرئيّ بأعلى الصفحة بالعربية + رابط
//      للتعليمات + النص الإنجليزي للأخطاء التقنية.

(async function () {
  let baseUrl;
  try {
    const cfg = await fetch('appsettings.json').then(r => r.json());
    baseUrl = cfg?.EjarApi?.BaseUrl;
  } catch {
    return showBanner('تعذّر قراءة appsettings.json — تأكّد من نشر الملف مع التطبيق.');
  }

  if (!baseUrl) {
    return showBanner('EjarApi.BaseUrl غير مضبوط في appsettings.json.');
  }

  const pageProto = location.protocol; // "https:" أو "http:"
  const apiProto  = baseUrl.startsWith('https://') ? 'https:'
                  : baseUrl.startsWith('http://')  ? 'http:'
                  : null;

  // ── Trap 1: mixed content ────────────────────────────────────────────
  if (pageProto === 'https:' && apiProto === 'http:') {
    return showBanner(
      'الـ PWA يعمل على HTTPS لكن BaseUrl للخدمة الخلفية يبدأ بـ http:// — ' +
      'المتصفّحات الحديثة تحجب هذا (Mixed Content). ' +
      `بدّل BaseUrl إلى https:// (المنشور حالياً: ${escapeHtml(baseUrl)}).`,
      baseUrl);
  }

  // ── Trap 2: لم يُعدَّل BaseUrl ────────────────────────────────────────
  if (baseUrl === 'https://api.ejar.ye') {
    return showBanner(
      'BaseUrl لم يُعدَّل بعد النشر — لا تزال القيمة الافتراضية ' +
      '«https://api.ejar.ye». حدّث ملف <code>wwwroot/appsettings.json</code> ' +
      'إلى عنوان خدمتك الحقيقيّ ثم أعد النشر.',
      baseUrl);
  }

  // ── Trap 3: اختبار fetch فعلي على /healthz ───────────────────────────
  try {
    const url = baseUrl.replace(/\/$/, '') + '/healthz';
    const r = await fetch(url, { method: 'GET', mode: 'cors' });
    if (!r.ok) {
      return showBanner(
        `الخدمة الخلفية ردّت بـ ${r.status} ${r.statusText} — تحقّق من ` +
        `سجلّ الخدمة، ربما لم يكتمل بدء التشغيل أو DB غير متاح.`, url);
    }
  } catch (e) {
    const msg = String(e?.message ?? e);
    let hint;
    if (/Failed to fetch|NetworkError/i.test(msg)) {
      hint = 'الأسباب الشائعة:\n' +
             '• Mixed Content: PWA على https والخدمة على http\n' +
             '• CORS: الخدمة لا تقبل origin هذا الموقع — أضفه إلى ' +
             'Cors:AllowedOrigins على الخدمة\n' +
             '• الخدمة غير متاحة من الإنترنت';
    } else if (/CORS/i.test(msg)) {
      hint = 'CORS رفض الطلب. أضف origin هذا الموقع (' +
             escapeHtml(location.origin) +
             ') إلى Cors:AllowedOrigins على الخدمة الخلفية.';
    } else {
      hint = msg;
    }
    return showBanner(
      `فشل الاتصال بالخدمة الخلفية على ${escapeHtml(baseUrl)}.\n${hint}`,
      baseUrl);
  }

  // كل شيء جيّد — لا banner.

  function showBanner(message, baseUrl) {
    console.error('[Ejar diagnostics]', message);
    const wrap = document.createElement('div');
    wrap.id = 'ac-api-diag';
    Object.assign(wrap.style, {
      position: 'fixed', insetInlineStart: '0', insetInlineEnd: '0',
      top: '0', zIndex: '10001',
      background: '#fef2f2', borderBottom: '2px solid #b91c1c',
      color: '#991b1b', padding: '12px 16px',
      fontFamily: 'Cairo, sans-serif', fontSize: '14px',
      direction: 'rtl', textAlign: 'start', whiteSpace: 'pre-line'
    });
    wrap.innerHTML = `
      <strong style="display:block;margin-bottom:6px">⚠ مشكلة في الاتصال بخدمة إيجار</strong>
      <div>${message.replace(/\n/g, '<br>')}</div>
      ${baseUrl ? `<div style="margin-top:6px;font-size:12px;color:#7f1d1d">BaseUrl: <code>${escapeHtml(baseUrl)}</code></div>` : ''}
      <button id="ac-api-diag-close" style="margin-top:8px;padding:4px 10px;border:1px solid #b91c1c;background:transparent;color:#7f1d1d;border-radius:4px;cursor:pointer">إخفاء</button>`;
    function place() { document.body.appendChild(wrap); }
    if (document.readyState !== 'loading') place();
    else document.addEventListener('DOMContentLoaded', place);
    wrap.addEventListener('click', e => {
      if (e.target.id === 'ac-api-diag-close') wrap.remove();
    });
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => (
      { '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]));
  }
})();
