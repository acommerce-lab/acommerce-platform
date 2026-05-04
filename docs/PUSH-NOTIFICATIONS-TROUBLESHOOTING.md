# تَشخيص الإشعارات الـ Push (خارج التطبيق)

ثلاث طبقات يَجب أن تَعمل معاً. الفشل في أيّ واحدة يَكسر الإشعارات بصمت.

```
[ Browser permission ]
     │
[ FCM service worker registered + subscribed ]
     │
[ Token submitted to /me/push-subscription ]
     │
[ Backend stores token via IDeviceTokenStore ]
     │
[ Notification.Operations dispatches ⇒ FCM channel ⇒ device ]
```

## الفحص خطوة-خطوة

### 1. إذن المتصفّح
DevTools Console:
```js
Notification.permission   // expected: "granted"
```
- `"default"` ⇒ لم يُطلَب من المُستخدِم. الـ `MainLayout.OnAfterRenderAsync` يَستدعي `ejarNotify.requestPermission`. تَأكَّد من نقرة المُستخدِم على "Allow".
- `"denied"` ⇒ المُستخدِم رَفَض. لا حلّ برمجيّ — يَجب إعادة الإذن من إعدادات المتصفّح يدويّاً.
- iOS Safari: لا يَدعم Web Push إلّا في PWA installed بـ iOS 16.4+.

### 2. تَسجيل Service Worker + اشتراك FCM
DevTools → Application → Service Workers:
- يَجب أن يَظهر `firebase-messaging-sw.js` (أو ما يُماثله) `activated`.
- Application → Storage → IndexedDB → `firebase-messaging-database` يَحوي token.

لو غير مُسَجَّل:
```bash
ls Apps/Ejar/Customer/Frontend/Ejar.Web/wwwroot/firebase-messaging-sw.js
```
يَجب أن يكون موجوداً مع `firebase.initializeApp(...)` صحيح.

### 3. التَوكن وَصَل للـ backend
Network tab → فلتر `push-subscription`:
- POST `/me/push-subscription` مع `{ token, platform }` ⇒ 200.
- لو 401: المُستخدِم غير مُصادَق وقت الـ subscribe — أعِد بعد login.
- لو 200 لكن لا notifications: التَالي.

### 4. الـ token مَحفوظ في DB
```sql
SELECT * FROM DeviceTokens WHERE UserId = @userId;
```
- لا صفوف ⇒ `EjarDeviceTokenStore.RegisterAsync` لم يَنفّذ INSERT. تَفقَّد الـ logs.
- صفوف موجودة لكن `Disabled = 1` ⇒ FCM رَجَع `unregistered` سابقاً وأُلغي. اشترك من جديد.

### 5. FCM credentials في الـ backend
```bash
ls Apps/Ejar/Customer/Backend/Ejar.Api/Secrets/firebase-service-account.json
```
- غير موجود ⇒ FCM channel غير مُهَيَّأ. `INotificationChannel` ⇒ `IDeviceTokenStore` يُسَجَّل null والـ DeviceTokensController يَردّ 200 صامتاً (راجع `DeviceTokensController.cs:40`).
- الفحص:
  ```bash
  curl -X POST http://localhost:5300/diag/fcm-test \
    -H "Authorization: Bearer $JWT"
  ```
  يَجب أن يَردّ `{ ok: true, sent: N }`.

### 6. الـ notification.send فعليّاً يُرسَل
في الـ backend logs أبحث عن `notification.send` envelope:
```bash
grep "notification.send" Apps/Ejar/Customer/Backend/Ejar.Api/logs/ejar-*.log | tail -5
```
- لا entries ⇒ لا أحد يَستدعي `notification.send`. تَأكَّد من interceptors (مثلاً `chat.message` ⇒ `notification.send` عبر `ChatNotificationsComposition`).
- entries موجودة لكن لا توصيل ⇒ `FirebaseNotificationChannel.SendAsync` فشل. abc الـ logs للأخطاء `FCM error`.

## أكثر الأسباب شيوعاً

| المُلاحَظ | السَبب الأرجح |
|----------|-----------------|
| الإشعار يَصل والتَطبيق مفتوح، لكن لا يَظهر OS-level مع التَطبيق مُغلَق | service worker غير مُسَجَّل أو FCM credentials ناقصة |
| في Android Chrome يَعمل، iOS Safari لا | iOS لا يَدعم Web Push إلّا للـ PWA installed |
| أوّل push يَعمل ثمّ يَتَوَقَّف بعد ساعات | token expired — لا refresh logic. أضف `onTokenRefresh` handler |
| لا شيء على أيّ متصفّح | غالباً firebase-service-account.json غير موجود في الـ backend |

## مرجع الكود

- `Apps/Ejar/Customer/Shared/Ejar.Customer.UI/Services/FirebasePushService.cs` — client-side subscribe
- `Apps/Ejar/Customer/Shared/Ejar.Customer.UI/wwwroot/js/firebase-push.js` — JS bridge
- `libs/providers/Notifications/Firebase/.../FirebaseNotificationChannel.cs` — server-side send
- `libs/kits/Notifications/.../DeviceTokensController.cs` — register endpoint
