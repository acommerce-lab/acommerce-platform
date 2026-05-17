// نسخة الإنتاج: تستعمل قائمة الـ assets المولّدة من البناء (service-worker-assets.js)
// لتخزين الـ shell + كل ملفات .NET WASM، وتقدّم استراتيجية cache-first للأصول
// + network-first لاستدعاءات الـ API. مأخوذة من القالب الرسمي ومُكيَّفة.
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'ejar-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

async function onInstall(event) {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(p => p.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(p => p.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

// مَلَفّات تَتَبَدَّل مَع كلّ نَشر — لا تُخَزَّن أَبَداً. بِدون هذا
// appsettings.json (الَّذي يَحوي App.Version) و version.json يَبقَيان
// قَديمَين بَعد التَّحديث حَتّى cold start، فَيَظهَر رَقَم النَّسخَة
// القَديم في صَفحَة "حِسابي".
const noStorePaths = new Set([
    'appsettings.json',
    'version.json',
    'manifest.webmanifest',
    'install-prompt.js',
    'pwa-update.js',
    'version-check.js',
    'api-diagnostics.js'
]);

async function onFetch(event) {
    if (event.request.method !== 'GET') return fetch(event.request);

    // ① ملفّات حَيَويّة لِلتَّحديث ⇒ شَبَكَة دائِماً، مَع fallback لِلـ cache
    //    لَو offline.
    try {
        const url = new URL(event.request.url);
        const last = url.pathname.split('/').pop();
        if (url.origin === self.location.origin && noStorePaths.has(last)) {
            try {
                return await fetch(event.request, { cache: 'no-store' });
            } catch {
                const cache = await caches.open(cacheName);
                return (await cache.match(event.request)) || new Response('', { status: 504 });
            }
        }
    } catch { /* URL parse فَشَل — اُكمِل بِالسُّلوك العاديّ */ }

    // ② أيّ شَيء آخَر: cache-first ثُمَّ شَبَكَة (السُّلوك الأَصليّ).
    const shouldServeIndexHtml = event.request.mode === 'navigate';
    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);
    return cachedResponse || fetch(event.request);
}
