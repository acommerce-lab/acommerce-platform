// Service worker مخصَّص لإستلام رسائل FCM في الخلفيّة (التبويب مغلق
// أو المتصفّح خلف نافذة أخرى). FCM SDK يفتح هذا الملف تحت scope محدّد
// (/firebase-cloud-messaging-push-scope) فلا يتعارض مع service-worker.js
// المسؤول عن PWA shell.
//
// تنبيه: قراءة firebase-config.json تتمّ هنا (لا نزرع الإعداد مرّتين).
// لو الملف غير منشور (المضيف ما عدّ FCM)، نتجاهل بصمت.

importScripts('https://www.gstatic.com/firebasejs/10.13.2/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/10.13.2/firebase-messaging-compat.js');

self.addEventListener('install', e => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));

(async () => {
  try {
    const res = await fetch('/firebase-config.json', { cache: 'no-store' });
    if (!res.ok) return;
    const cfg = await res.json();
    if (!cfg || !cfg.apiKey || !cfg.messagingSenderId || !cfg.appId) return;
    firebase.initializeApp(cfg);
    const messaging = firebase.messaging();

    // background message handler — نظهر إشعار نظام التشغيل.
    messaging.onBackgroundMessage(payload => {
      const title = (payload && payload.notification && payload.notification.title) || 'إيجار';
      const body  = (payload && payload.notification && payload.notification.body)  || '';
      const data  = (payload && payload.data) || {};
      const url   = data.url || (data.conversationId ? `/chat/${data.conversationId}` : '/');

      self.registration.showNotification(title, {
        body,
        icon: '/icon-192.png',
        badge: '/icon-192.png',
        tag:   data.conversationId ? `chat:${data.conversationId}` : 'ejar',
        data:  { url },
      });
    });
  } catch (e) {
    // غير قاتل — يعني ببساطة FCM غير مفعَّل في هذا الإصدار.
    console.debug('[firebase-sw] skipped:', e && e.message);
  }
})();

// النقر على الإشعار يفتح المحادثة المعنيّة (أو الجذر).
self.addEventListener('notificationclick', event => {
  event.notification.close();
  const url = (event.notification.data && event.notification.data.url) || '/';
  event.waitUntil((async () => {
    const all = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const c of all) {
      if (c.url.includes(url) && 'focus' in c) return c.focus();
    }
    if (self.clients.openWindow) return self.clients.openWindow(url);
  })());
});
