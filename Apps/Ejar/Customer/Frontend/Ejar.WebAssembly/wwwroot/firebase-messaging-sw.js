// Service worker مخصَّص لإستلام رسائل FCM في الخلفيّة (التبويب مغلق
// أو المتصفّح خلف نافذة أخرى). FCM SDK يفتح هذا الملف تحت scope محدّد
// (/firebase-cloud-messaging-push-scope) فلا يتعارض مع service-worker.js
// المسؤول عن PWA shell.
//
// مهمّ: تسجيل firebase + onBackgroundMessage + push handlers يجب أن
// يكون **synchronous على مستوى أعلى** من السكربت — ليس داخل async/await.
// Chrome يرفض إضافة push event listener بعد initial evaluation:
//   "Event handler of 'push' event must be added on the initial evaluation
//    of worker script."
// لذلك نُضمّن الإعداد inline (الـ web config كلّه عامّ، آمن للزرع هنا).

importScripts('https://www.gstatic.com/firebasejs/10.13.2/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/10.13.2/firebase-messaging-compat.js');

self.addEventListener('install', e => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));

// تكوين Firebase web (مفاتيح عامّة فقط — لا تحوي service account).
// مزامن لكي يسجِّل SDK push handlers قبل أن ينتهي السكربت.
firebase.initializeApp({
  apiKey: "AIzaSyD1Xe6QeXtt5Vj3Df89gP6GqcgLS8sXo_0",
  authDomain: "ejar-7adcb.firebaseapp.com",
  projectId: "ejar-7adcb",
  storageBucket: "ejar-7adcb.firebasestorage.app",
  messagingSenderId: "300907085870",
  appId: "1:300907085870:web:61fb88a0b2043006f49fbf",
  measurementId: "G-YBTW61LV96"
});

const messaging = firebase.messaging();

// background message handler — نُظهر إشعار نظام التشغيل.
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
