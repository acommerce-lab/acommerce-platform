// Firebase Cloud Messaging bridge للويب. الهدف: استخراج رمز الجهاز
// (FCM token) وتسليمه لخلفيّة إيجار عبر POST /me/push-subscription،
// ثمّ التقاط رسائل foreground ليُظهرها التطبيق كـ in-app toast.
//
// متطلّبات تشغيل هذا الملف:
//   1) firebase-config.json في الجذر (يُحقن من المضيف عند النشر).
//   2) firebase-messaging-sw.js في الجذر — يفتحه FCM SDK تلقائياً عند
//      messaging.getToken({ swRegistration }) لإستلام الرسائل في الخلفيّة.
//   3) VAPID public key (هويّة موقعنا على FCM web push) — موجود ضمن
//      firebase-config.json كـ "vapidKey".
//
// نستعمل CDN modular SDK (gstatic) لا package.json — الـ Blazor WASM
// لا يستهلك npm، ووحدات JS ESM تعمل بشكل أنيق هنا.
//
// واجهة C#:
//   ejarFirebase.init(config)        → boolean (نجاح التهيئة)
//   ejarFirebase.requestToken(vapid) → string|null (FCM token)
//   ejarFirebase.onMessage(dotnet)   → يسجّل callback لرسائل foreground

window.ejarFirebase = (function () {
  let _app = null;
  let _messaging = null;
  let _swReg = null;

  async function loadModules() {
    // Static import URLs — متصفّح حديث + ESM. لو محظور (offline) نُعيد null.
    const [{ initializeApp }, { getMessaging, getToken, onMessage, isSupported }] = await Promise.all([
      import('https://www.gstatic.com/firebasejs/10.13.2/firebase-app.js'),
      import('https://www.gstatic.com/firebasejs/10.13.2/firebase-messaging.js'),
    ]);
    return { initializeApp, getMessaging, getToken, onMessage, isSupported };
  }

  async function init(config) {
    try {
      if (!config || !config.apiKey || !config.messagingSenderId || !config.appId) {
        console.info('[ejarFirebase] config ناقص — تخطّي التهيئة');
        return false;
      }
      const mods = await loadModules();
      const supported = await mods.isSupported();
      if (!supported) {
        console.info('[ejarFirebase] FCM غير مدعوم في هذا المتصفّح');
        return false;
      }
      _app = mods.initializeApp(config);
      _messaging = mods.getMessaging(_app);

      // سجّل service worker مخصّص للرسائل (firebase-messaging-sw.js).
      // SW الموجود (service-worker.js) لـ PWA shell — لا نخلطه برسائل FCM
      // حتى لا يتعارض manifest الـ scopes أو يُكسر الـ caching.
      if ('serviceWorker' in navigator) {
        try {
          _swReg = await navigator.serviceWorker.register('/firebase-messaging-sw.js',
            { scope: '/firebase-cloud-messaging-push-scope' });
        } catch (e) {
          console.warn('[ejarFirebase] فشل تسجيل firebase-messaging-sw.js:', e);
        }
      }

      // foreground messages — سنُمرّرها لـ .NET لاحقاً عبر onMessage.
      mods.onMessage(_messaging, payload => {
        try {
          const t = (payload && payload.notification && payload.notification.title) || 'إيجار';
          const b = (payload && payload.notification && payload.notification.body) || '';
          const data = payload && payload.data;
          if (window.ejarNotify && typeof window.ejarNotify.show === 'function') {
            window.ejarNotify.show(t, b, {
              tag: 'fcm', url: data && data.url, alwaysShow: false
            });
          }
          // لو التطبيق يستمع، نمرّر الحمولة كاملة
          if (typeof _onMsgCallback === 'function') _onMsgCallback(payload);
        } catch (e) { console.warn('[ejarFirebase] onMessage handler خطأ:', e); }
      });

      // خزِّن الدوالّ للاستعمال لاحقاً (requestToken)
      _modCache = mods;
      return true;
    } catch (e) {
      console.warn('[ejarFirebase] init فشل:', e);
      return false;
    }
  }

  let _modCache = null;
  let _onMsgCallback = null;

  async function requestToken(vapidKey) {
    try {
      if (!_messaging || !_modCache) return null;
      // permission lazy — نطلب فقط عند الحاجة (لا في init حتى لا يفاجَأ
      // المستخدم بـ prompt قبل ما يعرف لماذا).
      if (!('Notification' in window)) return null;
      let perm = Notification.permission;
      if (perm === 'default') {
        try { perm = await Notification.requestPermission(); }
        catch { perm = 'denied'; }
      }
      if (perm !== 'granted') return null;

      // VAPID key Pre-flight: يَجب أن يَكون مفتاح Web Push public من
      // Firebase Console ⇒ Project Settings ⇒ Cloud Messaging ⇒ Web Push
      // certificates ⇒ Generate key pair. الشكل: 87-88 base64url يَبدأ بـ 'B'.
      // مَفتاح خاطئ يُنتج DOMException 'applicationServerKey is not valid'.
      if (!vapidKey || vapidKey.length < 80 || vapidKey.length > 100 || vapidKey[0] !== 'B') {
        console.error(
          '[ejarFirebase] VAPID key غير صالح (الطول=' + (vapidKey?.length ?? 0) +
          '، الحرف الأوّل=' + (vapidKey?.[0] ?? '?') + '). ' +
          'احصل على المفتاح من Firebase Console → Project Settings → Cloud Messaging → ' +
          'Web Push certificates → Generate key pair → انسخ Public key (88 char يَبدأ بـ B) ' +
          'وضعه في wwwroot/firebase-config.json تحت "vapidKey".');
        return null;
      }

      const token = await _modCache.getToken(_messaging, {
        vapidKey: vapidKey,
        serviceWorkerRegistration: _swReg || undefined,
      });
      return token || null;
    } catch (e) {
      console.error('[ejarFirebase] getToken فشل — غالباً VAPID key مَرفوض. ' +
        'تَأكَّد من Firebase Console > Cloud Messaging > Web Push certificates.', e);
      return null;
    }
  }

  function onMessage(dotnetRef, methodName) {
    _onMsgCallback = function (payload) {
      try { dotnetRef.invokeMethodAsync(methodName || 'OnFcmForeground', payload); }
      catch (e) { console.warn('[ejarFirebase] تمرير onMessage إلى .NET فشل:', e); }
    };
  }

  // قراءة الإعداد من same-origin مباشرةً عبر window.fetch — نتجنّب
  // HttpClient في .NET الذي عنده BaseAddress = API على origin مختلف،
  // فيطلب /firebase-config.json من ejarapi بدل ejarpwa فيرجع 404.
  async function initFromUrl(url) {
    try {
      const res = await fetch(url || '/firebase-config.json', { cache: 'no-store' });
      if (!res.ok) return false;
      const cfg = await res.json();
      const ok = await init(cfg);
      return ok ? cfg : false;
    } catch (e) {
      console.warn('[ejarFirebase] initFromUrl فشل:', e);
      return false;
    }
  }

  return { init, initFromUrl, requestToken, onMessage };
})();
