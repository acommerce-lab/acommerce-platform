# Secrets — ملف اعتماد Firebase Admin SDK

هذا المجلَّد مخصَّص لـ **firebase-service-account.json** الذي يحوي المفتاح الخاصّ لخادم Firebase Cloud Messaging Admin SDK.

> ⚠️ الملف الحقيقيّ **لا** يُلتزَم في git — تحجبه قواعد GitHub Push Protection لأنّه مفتاح خاصّ. ضمن المستودع تجد فقط `firebase-service-account.example.json` كنموذج لشكل الملف.

## كيف تضع الملف؟

### للتطوير المحلّيّ
1. من Firebase Console → Project Settings → **Service accounts** → "Generate new private key" → نزّل JSON.
2. ضعه باسم `firebase-service-account.json` في **هذا** المجلَّد.
3. شغّل `dotnet run --project Apps/Ejar/Customer/Backend/Ejar.Api`. المسار النسبيّ في `appsettings.json` يحلّ تلقائياً على ContentRoot.

في Log الإقلاع تظهر سطر:
```
Ejar.Firebase: registered FCM channel + EjarDeviceTokenStore (creds=/abs/path/Secrets/firebase-service-account.json)
```

### للنشر (runasp.net / IIS / Linux)
خياران — اختر واحداً:

**(أ) رفع الملف مع التطبيق** — أبسط
- ضع الملف في مجلَّد `Secrets/` على الخادم (نفس مكان `appsettings.json`، أي `ContentRoot`).
- على runasp.net: استخدم File Manager للرفع تحت `wwwroot/../Secrets/firebase-service-account.json`.
- المسار النسبيّ في `appsettings.json` يستلم تلقائياً.

**(ب) متغيِّر بيئة** — أنظف لمن يريد فصل الأسرار عن الـ filesystem
- افتح JSON بمحرّر، انسخ كامل المحتوى (سطر واحد طويل).
- على الخادم اضبط: `Notifications__Firebase__CredentialsJson=<JSON>`
- يتجاوز `CredentialsFilePath` تلقائياً (راجع `FirebaseNotificationChannel.InitializeMessaging`).

> إن لم تضع الملف ولا المتغيّر، سيُسجِّل التطبيق سطراً واحداً ويتجاوز FCM:
> `Ejar.Firebase: skipped — set Notifications:Firebase:CredentialsJson or :CredentialsFilePath to enable`
> الباقي (realtime + إشعارات DB) يبقى يعمل بشكل طبيعيّ.

## VAPID public key

الـ VAPID للواجهة موجود في `Apps/Ejar/Customer/Frontend/Ejar.WebAssembly/wwwroot/firebase-config.json` تحت `vapidKey`. هذا مفتاح **عامّ** فيُلتزَم في git بشكل عاديّ.

## المشروع الحاليّ

- Project ID: `ejar-7adcb`
- Service account: `firebase-adminsdk-fbsvc@ejar-7adcb.iam.gserviceaccount.com`
- VAPID public key: `Wv9jzMbpoAopOPJAlhGNsKEEVm5X_jwdZLoICYqLYSQ` (في `firebase-config.json`)
