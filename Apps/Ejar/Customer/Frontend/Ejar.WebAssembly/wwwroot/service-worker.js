// Service worker للتطوير. إستراتيجية: network-first مع cache-fallback
// خفيف. مهمّ لإسقاط أيّ نسخة قديمة من install-prompt.js / index.html
// عالقة في cache المتصفّح بعد إعادة النشر.
//
// VERSION هنا — كلّما تغيّر نُجبر المتصفّح على تحديث الـ SW وحذف cache
// القديم. ارفعه يدوياً عند كل تغيير في PWA shell (manifest/icons/SW).
const VERSION = 'ejar-pwa-v4-2026-04-27';
const SHELL_CACHE = `shell-${VERSION}`;

// عند التثبيت: skipWaiting → الـ SW الجديد يأخذ السيطرة فوراً بدل أن
// ينتظر إغلاق كل التبويبات. مهمّ ليرى المستخدم التغيّرات بعد كل نشر.
self.addEventListener('install', e => {
  self.skipWaiting();
});

// عند التفعيل: نظّف caches الإصدارات السابقة + claim كل العملاء حالاً.
self.addEventListener('activate', e => {
  e.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys
      .filter(k => k.startsWith('shell-') && k !== SHELL_CACHE)
      .map(k => caches.delete(k)));
    await self.clients.claim();
  })());
});

// fetch handler ضرورة لتمرير معايير تثبيت Chrome PWA. الإستراتيجية:
//   - GET فقط (POST/PATCH تتجاوز SW)
//   - navigation request → network-first، fallback لـ index.html من cache
//   - أصول ثابتة (_framework / _content / *.png / *.svg) → stale-while-revalidate
//   - بقيّة → network-first
//
// appsettings.json + install-prompt.js → دائماً من الشبكة (no-cache) ليأخذ
// المستخدم آخر إعدادات البيئة وآخر نسخة من سكربت التثبيت بعد كل نشر.
self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') return;
  const url = new URL(event.request.url);

  // ① ملفات تتبدّل مع كل نشر — لا تُخزّن أبداً
  if (url.pathname === '/appsettings.json'
      || url.pathname === '/install-prompt.js'
      || url.pathname === '/api-diagnostics.js'
      || url.pathname === '/version-check.js'
      || url.pathname === '/version.json'
      || url.pathname === '/manifest.webmanifest'
      || url.pathname === '/diagnose.html'
      || url.pathname === '/reset.html') {
    event.respondWith(fetch(event.request, { cache: 'no-store' })
      .catch(() => caches.match(event.request)));
    return;
  }

  // ② navigation → network-first، lo offline ⟶ index.html من cache
  if (event.request.mode === 'navigate') {
    event.respondWith((async () => {
      try {
        const fresh = await fetch(event.request);
        const cache = await caches.open(SHELL_CACHE);
        cache.put('/', fresh.clone());
        return fresh;
      } catch {
        return (await caches.match('/')) || new Response(
          '<h1>إيجار غير متاح حالياً</h1>',
          { headers: { 'Content-Type': 'text/html; charset=utf-8' } });
      }
    })());
    return;
  }

  // ③ أصول ثابتة (Blazor framework + content) → cache-first مع تحديث
  if (url.pathname.startsWith('/_framework/')
      || url.pathname.startsWith('/_content/')) {
    event.respondWith((async () => {
      const cached = await caches.match(event.request);
      const fetchPromise = fetch(event.request).then(r => {
        if (r.ok) {
          caches.open(SHELL_CACHE).then(c => c.put(event.request, r.clone()));
        }
        return r;
      }).catch(() => cached);
      return cached || fetchPromise;
    })());
    return;
  }

  // ④ غير ذلك (e.g. icons، favicon) → network-first
  event.respondWith(
    fetch(event.request).catch(() => caches.match(event.request)
      || new Response('', { status: 504 }))
  );
});

// رسالة من الصفحة لإجبار SW على skipWaiting (لو نسخة جديدة تنتظر)
self.addEventListener('message', e => {
  if (e.data === 'skipWaiting') self.skipWaiting();
});
