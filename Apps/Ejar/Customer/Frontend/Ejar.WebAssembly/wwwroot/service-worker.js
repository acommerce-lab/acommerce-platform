// Service worker للتطوير — لا يخزّن شيئاً (يمرّ كل الطلبات للشبكة).
// النسخة الإنتاجية الكاملة: انظر service-worker.published.js التي تُولَّد
// آلياً عند publish وتدعم تشغيل التطبيق offline.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { /* dev: pass-through */ });
