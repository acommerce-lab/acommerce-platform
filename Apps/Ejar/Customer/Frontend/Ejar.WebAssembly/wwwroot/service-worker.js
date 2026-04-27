// Service worker للتطوير. لتمرير معايير تثبيت PWA على Chrome/Android
// يجب أن يحوي fetch handler يستدعي respondWith فعلاً (لا مجرد listener
// فارغ) — وإلا يكتفي المتصفّح بعرض «Add to Home Screen» (اختصار) بدل
// «Install app» (تطبيق مستقلّ في نافذته).
//
// الإستراتيجية في dev: pass-through للشبكة دون cache (السكربت الإنتاجي
// service-worker.published.js يفعل cache-first بعد publish).

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));

self.addEventListener('fetch', (event) => {
  // فقط GET — POST/PATCH/الخ تتجاوز SW.
  if (event.request.method !== 'GET') return;

  // navigation requests: نريد start_url يعمل offline لاحقاً، لكن هنا في dev
  // نمرّرها فقط للشبكة. الإنتاج يخدم index.html من الـ cache.
  event.respondWith(
    fetch(event.request).catch(() =>
      // fallback غير ضارّ لو الشبكة مقطوعة — يمنع SW من الانهيار صامتاً.
      new Response('', { status: 504, statusText: 'Network unavailable' })
    )
  );
});
