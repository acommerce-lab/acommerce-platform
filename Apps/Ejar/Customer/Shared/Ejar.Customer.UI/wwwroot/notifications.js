// إشعارات المتصفّح الأصليّة (Web Notification API). تُظهر toast على مستوى
// نظام التشغيل عندما يكون التطبيق مفتوحاً لكن غير مرئي (تبويب آخر، نافذة
// أخرى، الهاتف مقفول). لا تتطلّب أيّ خادم خارجيّ ولا VAPID — تعمل فوراً.
//
// لإشعارات تصل عندما يكون التبويب **مغلقاً**: تتطلّب Web Push API
// (Service Worker + push subscription + VAPID + خادم push). تكاملها مع
// FCM موجود في libs/kits/Notifications/Providers/Firebase لكنّه يحتاج:
//   1) Firebase project + Service Account JSON على الـ Backend
//   2) VAPID public key يُرسَل للواجهة
//   3) Service worker register يستدعي pushManager.subscribe
//   4) إرسال الـ subscription إلى الـ Backend وتخزينه per-user
// نلجأ إليه عند توفّر هذه المتطلّبات. النسخة الحاليّة تستخدم الـ
// Notification API المباشر فقط.

window.ejarNotify = (function () {
  function permission() {
    try { return Notification.permission || 'default'; }
    catch { return 'unsupported'; }
  }

  async function requestPermission() {
    if (!('Notification' in window)) return 'unsupported';
    if (Notification.permission === 'granted') return 'granted';
    if (Notification.permission === 'denied')  return 'denied';
    try {
      const result = await Notification.requestPermission();
      return result;
    } catch { return 'denied'; }
  }

  function show(title, body, opts) {
    if (!('Notification' in window) || Notification.permission !== 'granted') return false;
    // لا تظهر الإشعار لو التبويب مرئيّ (المستخدم يرى الرسالة بالفعل).
    if (document.visibilityState === 'visible' && !(opts && opts.alwaysShow)) return false;
    try {
      const n = new Notification(title || 'إيجار', {
        body: body || '',
        icon: (opts && opts.icon) || '/icon-192.png',
        tag:  (opts && opts.tag)  || 'ejar',
        renotify: !!(opts && opts.renotify),
      });
      const url = opts && opts.url;
      if (url) {
        n.onclick = () => {
          window.focus();
          window.location.href = url;
          n.close();
        };
      }
      return true;
    } catch (e) {
      console.warn('[ejarNotify] show failed:', e);
      return false;
    }
  }

  return { permission, requestPermission, show };
})();
