// Service worker للتطوير. إستراتيجية: network-first مع cache-fallback
// خفيف. مهمّ لإسقاط أيّ نسخة قديمة من install-prompt.js / index.html
// عالقة في cache المتصفّح بعد إعادة النشر.
//
// VERSION هنا — كلّما تغيّر نُجبر المتصفّح على تحديث الـ SW وحذف cache
// القديم. ارفعه يدوياً عند كل تغيير في PWA shell (manifest/icons/SW).
const VERSION = 'ejar-pwa-v39-2026-05-01';
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

// تخزين آمن: caches.put يرفض على responses معيّنة (206 range, opaque cross-origin,
// status != 200..299) وعلى مخططات غير http/https. نتحقّق أوّلاً ثمّ نلتقط أيّ خطأ
// متبقّي حتى لا يظهر unhandledrejection في الكونسول.
async function safePut(cacheName, request, response) {
  try {
    if (!request || !response) return;
    if (request.method !== 'GET') return;
    const url = new URL(request.url);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') return;
    if (!response.ok) return;          // يستثني opaque (status=0) و 5xx/4xx
    if (response.status === 206) return; // partial content غير قابل للتخزين
    const cache = await caches.open(cacheName);
    await cache.put(request, response);
  } catch (e) {
    // فشل غير قاتل — نمرّ بصمت (chrome-extension، quota exceeded، …).
    console.debug('[sw] cache.put skipped:', e && e.message);
  }
}

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
  let url;
  try { url = new URL(event.request.url); }
  catch { return; }
  // نتجاوز كلّ ما ليس http(s) (chrome-extension://، blob://، …)
  if (url.protocol !== 'http:' && url.protocol !== 'https:') return;

  // ⚠️ تجاوز كلّ طلبات cross-origin بالكامل — لا يلمسها الـ SW. بدون هذا
  // SignalR negotiate إلى ejarapi.runasp.net (cross-origin مع credentials)
  // يمرّ خلال SW ويفقد بعض الـ CORS metadata في عدد من المتصفّحات، فيظهر
  // "No Access-Control-Allow-Origin header" رغم أنّ الباك مكوَّن CORS بشكل
  // سليم. الطلبات مباشرةً من الـ page تذهب إلى API بدون وساطة وتعمل تماماً.
  // SW يعتني فقط بـ same-origin (الـ shell + الأصول الثابتة).
  if (url.origin !== self.location.origin) return;

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
      .catch(() => caches.match(event.request)
        .then(cached => cached || new Response('', { status: 504 }))));
    return;
  }

  // ② navigation → network-first، lo offline ⟶ index.html من cache
  if (event.request.mode === 'navigate') {
    event.respondWith((async () => {
      try {
        const fresh = await fetch(event.request);
        await safePut(SHELL_CACHE, new Request('/'), fresh.clone());
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
        // fire-and-forget مع safePut يلتقط الأخطاء داخلياً
        safePut(SHELL_CACHE, event.request, r.clone());
        return r;
      }).catch(() => cached);
      return cached || fetchPromise;
    })());
    return;
  }

  // ④ غير ذلك (e.g. icons، favicon) → network-first
  event.respondWith((async () => {
    try { return await fetch(event.request); }
    catch {
      return (await caches.match(event.request))
        || new Response('', { status: 504 });
    }
  })());
});

// رسالة من الصفحة لإجبار SW على skipWaiting (لو نسخة جديدة تنتظر)
self.addEventListener('message', e => {
  if (e.data === 'skipWaiting') self.skipWaiting();
});

// النقر على إشعار صادر عن ejarNotify.show (الذي يستخدم
// ServiceWorkerRegistration.showNotification بدل new Notification،
// لأنّ Chrome/Edge على Android يمنع الـ constructor المباشر). نفتح
// التبويب الموجود إن كان مطابقاً للـ url، وإلا نفتح نافذة جديدة.
self.addEventListener('notificationclick', event => {
  event.notification.close();
  const url = (event.notification.data && event.notification.data.url) || '/';
  event.waitUntil((async () => {
    try {
      const all = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
      // ابحث عن تبويب على نفس origin — أعطه focus وانقله إلى الـ url.
      for (const c of all) {
        if (c.url && c.url.includes(self.location.origin)) {
          if ('focus' in c) await c.focus();
          if ('navigate' in c && url) { try { await c.navigate(url); } catch (_) {} }
          return;
        }
      }
      // لا تبويب مفتوح → افتح واحداً جديداً.
      if (self.clients.openWindow) await self.clients.openWindow(url);
    } catch (e) { /* noop */ }
  })());
});
