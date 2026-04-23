# Ashare Legacy Backend - Feature Inventory

**Service**: `/tmp/ACommerce.Libraries/Apps/Ashare.Api/`  
**Purpose**: Shared spaces booking & rental platform (Ashare - عشير)  
**Tech Stack**: ASP.NET Core 8, EF Core, SignalR, Firebase, Noon Payments

---

## ملخص الخدمة

خدمة Ashare تدير منصة حجز المساحات المشتركة (سكنية وتجارية) وتوفر:
- مصادقة 2FA عبر نفاذ (Nafath)
- إدارة الحجوزات والدفع (Noon)
- إشعارات فورية (SignalR + Firebase)
- بحث متقدم ومحادثات فورية

---

## المتحكمات (Controllers)

### AuthController - المصادقة والدخول

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| POST | `/api/auth/nafath/initiate` | بدء مصادقة نفاذ برقم الهوية | Anonymous | ITwoFactorAuthenticationProvider, IMarketingEventTracker |
| GET | `/api/auth/nafath/status` | التحقق من حالة المصادقة (polling) | Anonymous | ITwoFactorSessionStore |
| POST | `/api/auth/nafath/complete` | إكمال المصادقة وإنشاء JWT token | Anonymous | IAuthenticationProvider, IBaseAsyncRepository<Profile> |
| POST | `/api/auth/admin/login` | دخول المشرف بكلمة السر | Anonymous | IAuthenticationProvider |
| GET | `/api/auth/me` | جلب معلومات المستخدم الحالي | User | IBaseAsyncRepository<Profile> |
| POST | `/api/auth/logout` | تسجيل الخروج | User | None |

**الملاحظات**: 
- المصادقة الأساسية عبر Nafath 2FA (رقم هوية وتطبيق نفاذ)
- ينشئ/يحدث بروفايل عند أول دخول (تسجيل تلقائي)
- يتتبع حدث تسجيل/دخول للتسويق

---

### NotificationsController - إدارة الإشعارات للمستخدم

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| POST | `/api/notifications/device-token` | تسجيل Firebase device token | User | IFirebaseTokenStore |
| DELETE | `/api/notifications/device-token` | إلغاء تسجيل جهاز | User | IFirebaseTokenStore |
| GET | `/api/notifications/devices/count` | عدد الأجهزة المسجلة للمستخدم | User | IFirebaseTokenStore |
| POST | `/api/notifications/test` | إرسال إشعار اختباري | User | INotificationService |
| GET | `/api/notifications/settings` | جلب إعدادات الإشعارات | User | None |
| PUT | `/api/notifications/settings` | تحديث إعدادات الإشعارات | User | None |

**الملاحظات**:
- يدعم iOS و Android و Web platforms
- يحفظ device metadata (app version, device model)
- الإعدادات غير محفوظة حالياً في DB (TODO)

---

### AdminNotificationsController - إرسال إشعارات من لوحة التحكم

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| GET | `/api/admin/notifications/users` | قائمة المستخدمين مع أجهزتهم | Admin | DbContext (Profile, DeviceTokenEntity) |
| POST | `/api/admin/notifications/send` | إرسال إشعار لمستخدمين محددين | Admin | INotificationService, DeviceTokenEntity |
| POST | `/api/admin/notifications/broadcast` | إرسال إشعار لجميع المستخدمين | Admin | INotificationService, DeviceTokenEntity |
| GET | `/api/admin/notifications/stats` | إحصائيات الإشعارات (مستخدمين/أجهزة) | Admin | DbContext (Profile, DeviceTokenEntity) |
| GET | `/api/admin/notifications/test` | اختبار الاتصال بـ DB | Anonymous | DbContext |
| POST | `/api/admin/notifications/test-firebase` | اختبار إرسال Firebase عبر NotificationService | Anonymous | INotificationService |
| POST | `/api/admin/notifications/test-firebase-direct` | اختبار Firebase مباشر (bypass NotificationService) | Anonymous | FirebaseMessagingService |

**الملاحظات**:
- يدعم أنواع إشعارات (info, warning, error, success, promotion, system)
- يدعم أولويات (low, normal, high, critical)
- يرسل عبر InApp (SignalR) + Firebase (push)
- يرسل بيانات إضافية مخصصة
- endpoints اختبار للتشخيص بدون مصادقة

---

### MediaController - رفع تحميل الصور

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| POST | `/api/media/upload` | رفع صورة واحدة | User | IStorageProvider |
| POST | `/api/media/upload/multiple` | رفع عدة صور (حد أقصى 5) | User | IStorageProvider |
| GET | `/api/media/{directory}/{fileName}` | تحميل صورة | Anonymous | IStorageProvider (Google Cloud / Alibaba OSS) |
| GET | `/api/media/{directory}/{subDirectory}/{fileName}` | تحميل صورة من مسار متداخل | Anonymous | IStorageProvider |

**الملاحظات**:
- الأنواع المدعومة: JPEG, PNG, GIF, WebP
- أقصى حجم: 10 MB (ملف واحد)، 50 MB (عدة ملفات)
- المجلدات المسموحة: listings, profiles, vendors
- يفرض HTTPS في الإنتاج (X-Forwarded-Proto fallback)

---

### PaymentCallbackController - معالجة استدعاءات الدفع

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| GET | `/host/payment/callback` | استدعاء بوابة الدفع (Noon) - GET | Anonymous | IHubContext<NotificationHub> |
| POST | `/host/payment/callback` | استدعاء بوابة الدفع (Noon) - POST | Anonymous | IHubContext<NotificationHub> |

**الملاحظات**:
- يتلقى: orderId, status, transactionId, resultCode, message, error
- يرسل إشعار SignalR فوراً للتطبيق (مجموعة payment_{orderId})
- يعيد صفحة HTML تشير المستخدم لإغلاق المتصفح
- يدعم "success"/"captured"/resultCode="0" كنجاح

---

### ErrorReportingController - تقارير الأخطاء من التطبيقات

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| POST | `/api/errorreporting/report` | استقبال تقرير خطأ من التطبيق | Anonymous | SMTP (Email) |

**البيانات المستقبلة**:
- source, operation, errorMessage, stackTrace
- platform (iOS/Android/Web), appVersion, osVersion, deviceModel
- additionalData (custom JSON)

**الملاحظات**:
- يرسل email HTML منسق بـ SMTP (async)
- يسجل مع ReportId فريد

---

### AttributionController - تتبع الحملات التسويقية

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| POST | `/api/marketing/attribution` | التقاط بيانات إسناد من JS/تطبيق | Anonymous | IAttributionService |
| POST | `/api/marketing/attribution/associate` | ربط مستخدم بجلسة إسناد بعد الدخول | Anonymous | IAttributionService |
| GET | `/api/marketing/attribution/{sessionId}` | جلب بيانات إسناد الجلسة | Anonymous | IAttributionService |

**البيانات**:
- utmSource, utmMedium, utmCampaign, clickId
- sessionId (من cookie ashare_session أو header X-Session-Id)
- referrerUrl, deviceType, platform

**الملاحظات**:
- ينشئ sessionId جديد إذا لم يكن موجوداً
- يثري البيانات من HTTP headers (Referer, User-Agent)

---

### CategoryAttributesController - خصائص الفئات

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| GET | `/api/categoryattributes/mappings` | جميع ربطات الفئات بالخصائص | Anonymous | IRepositoryFactory<CategoryAttributeMapping> |
| GET | `/api/categoryattributes/category/{categoryId}` | خصائص فئة معينة مع القيم | Anonymous | IRepositoryFactory<CategoryAttributeMapping, AttributeDefinition> |
| GET | `/api/categoryattributes/categories` | قائمة الفئات المتاحة | Anonymous | IRepositoryFactory<ProductCategory> |
| GET | `/api/categoryattributes/debug/all-attributes` | تصحيح: جميع الخصائص | Anonymous | IRepositoryFactory<AttributeDefinition> |

**الملاحظات**:
- الفئات: سكني، طلب سكن، طلب شريك، إداري، تجاري
- الخصائص: title, price, duration, timeUnit, location, images، إلخ
- يتضمن قيم الخصائص (colorHex, imageUrl, etc.)

---

### NafathWebhookController - Webhook نفاذ

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| POST | `/api/auth/nafath/webhook` | استدعاء Webhook من نفاذ عند اكتمال المصادقة | Webhook Signature | ITwoFactorAuthenticationProvider |

**الملاحظات**:
- وراثة من NafathWebhookControllerBase
- معالجة توقيع الـ webhook من نفاذ
- يتم توجيهه من NafathTwoFactorProvider

---

### MigrationController - ترحيل البيانات

| الـ Method | الـ Route | الوصف | المصادقة | الاعتماديات |
|-----------|---------|--------|---------|------------|
| POST | `/api/migration/offers/seed` | بذر العروض من البيانات الثابتة | Admin | OffersMigrationService |
| POST | `/api/migration/offers/reseed` | حذف وإعادة بذر العروض مع تحميل الصور | Admin | OffersMigrationService |

**الملاحظات**:
- يجلب من API القديم (ashare-001-site4.mtempurl.com)
- يحمل الصور من ashare-001-site6.mtempurl.com/Images/
- يدعم fallback للبيانات الثابتة
- يحفظ في Google Cloud / Alibaba OSS

---

## الخدمات (Services)

### AshareNotificationService - خدمة الإشعارات

```csharp
SendNewMessageNotificationAsync(userId, senderName, messagePreview, chatId)
SendNewBookingNotificationAsync(hostUserId, spaceName, customerName, bookingId)
SendBookingConfirmedNotificationAsync(customerUserId, spaceName, bookingId)
SendBookingRejectedNotificationAsync(customerUserId, spaceName, reason, bookingId)
SendBookingCancelledNotificationAsync(userId, spaceName, bookingId, isHost)
```

**الملاحظات**:
- يستخدم INotificationService تحتها
- يرسل عبر InApp + Firebase
- يضع actionUrl و data مخصصة

---

### AshareSeedDataService - بيانات البذر الأولية

**الفئات المعرفة مسبقاً**:
- Residential (سكني)
- LookingForHousing (طلب سكن)
- LookingForPartner (طلب شريك)
- Administrative (إداري)
- Commercial (تجاري)

**الخصائص**:
- Common: Title, Description, Price, Duration, TimeUnit, Location, City, Images
- Residential: PropertyType, UnitType, Floor, BillType, RentalType, Area, Rooms, Bathrooms, Furnished, Amenities, Contact preferences
- Partner: PersonalName, Age, Gender, Nationality, Job, MinPrice, MaxPrice, Smoking
- Commercial: CommercialPropertyType, Capacity, Parking, WorkingHours, Facilities

---

### CacheWarmupService - تدفئة الذاكرة المؤقتة

**ينشط تلقائياً بعد 5 ثوانٍ من الـ startup** (معلق حالياً):

```
GET /api/listings/featured?limit=10
GET /api/listings/new?limit=10
GET /api/listings?limit=50
POST /api/listings/search (مع فلاتر IsActive=true, Status=1)
```

**الملاحظات**:
- يسخن الذاكرة المؤقتة للقوائم الأساسية
- يسجل الـ elapsed time لكل endpoint

---

### OffersMigrationService - ترحيل العروض

```csharp
DeleteAndReseedOffersAsync()
SeedOffersFromStaticDataAsync()
FetchOffersListAsync()
FetchOfferDetailsAsync()
SaveOffersWithFullDetailsAsync()
```

**الملاحظات**:
- يجلب من API القديم مع fallback للبيانات الثابتة
- يحمل الصور إلى IStorageProvider (GCS / Alibaba OSS)
- يحفظ في ProductListing مع صور محملة

---

## المتحكمات من ACommerce Library

يستخدم Ashare المتحكمات التالية من مكتبة ACommerce:

| المتحكم | الـ Route | الغرض |
|--------|---------|--------|
| ProfilesController | `/api/profiles/*` | إدارة البروفايلات |
| VendorsController | `/api/vendors/*` | إدارة البائعين |
| ProductsController | `/api/products/*` | إدارة المنتجات/المساحات |
| ProductListingsController | `/api/listings/*` | عروض المساحات (الحجوزات) |
| OrdersController | `/api/orders/*` | إدارة الطلبات |
| PaymentsController | `/api/payments/*` | إدارة الدفع (Noon) |
| ChatsController | `/hubs/chat` | المحادثات الفورية |
| BookingsController | `/api/bookings/*` | إدارة الحجوزات |
| DashboardController | `/api/dashboard/*` | لوحة تحكم المشرف |
| AdminOrdersController | `/api/admin/orders/*` | طلبات إدارية |
| AdminListingsController | `/api/admin/listings/*` | عروض إدارية |
| ReportsController | `/api/reports/*` | تقارير |
| ComplaintsController | `/api/complaints/*` | الشكاوى |
| SubscriptionsController | `/api/subscriptions/*` | الاشتراكات |
| DocumentTypesController | `/api/document-types/*` | أنواع المستندات |
| LocationSearchController | `/api/locations/*` | البحث الجغرافي |
| AttributeDefinitionsController | `/api/attributes/*` | تعريفات الخصائص |
| UnitsController | `/api/units/*` | الوحدات |
| CurrenciesController | `/api/currencies/*` | العملات |
| ContactPointsController | `/api/contact-points/*` | نقاط الاتصال |
| AppVersionsController | `/api/versions/*` | إصدارات التطبيق |
| AuditLogController | `/api/audit-logs/*` | سجل التدقيق |
| LegalPagesController | `/api/legal/*` | الصفحات القانونية |
| MarketingStatsController | `/api/marketing/stats/*` | إحصائيات التسويق |

---

## Cross-Cutting Concerns

### المصادقة والتفويض
- **Nafath 2FA**: مصادقة رقم هوية عبر تطبيق نفاذ الحكومي
- **JWT Tokens**: توكنات JWT للجلسات المصرح بها
- **Claims-based Authorization**: Roles (Admin, User)
- **Webhook Signature Verification**: توقيع نفاذ webhook

### الإشعارات
- **Firebase Cloud Messaging**: push notifications للأجهزة المحمولة
- **SignalR Hubs**:
  - `NotificationHub` (/hubs/notifications): إشعارات فورية + payment callbacks
  - `ChatHub` (/hubs/chat): رسائل المحادثات الفورية
- **In-Memory Publisher**: نشر الرسائل داخل الخدمة
- **Device Token Storage**: Firebase DeviceTokenEntity في EF Core

### الدفع
- **Noon Payments Gateway**: معالجة المدفوعات والـ callbacks
- **Payment Callbacks**: redirect من Noon → /host/payment/callback
- **SignalR Payment Notifications**: إخطار فوري التطبيق بالنتيجة

### التخزين والملفات
- **Google Cloud Storage**: تخزين الصور (إنتاج)
- **Alibaba OSS**: تخزين بديل (Alibaba regions)
- **IStorageProvider**: واجهة موحدة للتخزين

### قاعدة البيانات
- **Entity Framework Core**: ORM
- **ApplicationDbContext**: السياق الرئيسي
- **DataProtectionKeyContext**: تخزين مفاتيح التشفير
- **Data Protection API**: تشفير البيانات الحساسة

### خدمات إضافية
- **Service Registry**: خدمة تسجيل الخدمات المركزي
- **In-Memory Messaging Bus**: نشر/اشتراك داخل العملية
- **Marketing Analytics**: تتبع أحداث التسويق (registration, login, attribution)
- **Meta Conversions**: تحويلات Meta pixel

### معالجة الأخطاء
- **GlobalExceptionMiddleware**: معالجة استثناءات HTTP عامة
- **SignalRExceptionFilter**: معالجة استثناءات SignalR Hub
- **Error Reporting**: تقارير الأخطاء من التطبيقات عبر SMTP

### التطوير والتصحيح
- **Serilog Logging**: سجل شامل (سجلات مفصلة)
- **Health Checks**: فحوصات صحة Middleware
- **Swagger/OpenAPI**: توثيق API

---

## Summary of Feature Count

| الفئة | العدد | الملاحظات |
|--------|--------|----------|
| **Auth Endpoints** | 6 | nafath initiate/status/complete, admin login, me, logout |
| **Notification Endpoints** | 6 | device-token (register/unregister), devices/count, test, settings (get/put) |
| **Admin Notifications Endpoints** | 7 | users, send, broadcast, stats, test, test-firebase, test-firebase-direct |
| **Media Endpoints** | 4 | upload, upload/multiple, get, get-nested |
| **Payment Callback Endpoints** | 2 | callback (GET/POST) |
| **Attribution Endpoints** | 3 | capture, associate, get-by-session |
| **Error Reporting Endpoints** | 1 | report |
| **Category Attributes Endpoints** | 4 | mappings, category/{id}, categories, debug/all-attributes |
| **Nafath Webhook Endpoints** | 1 | webhook (from NafathWebhookControllerBase) |
| **Migration Endpoints** | 2 | seed, reseed |
| **ACommerce Library Controllers** | 23 | profiles, vendors, products, listings, orders, etc. |
| **SignalR Hubs** | 2 | NotificationHub, ChatHub |
| **Background Services** | 1 | CacheWarmupService (معلق) |

**الإجمالي**: 48 custom endpoint + 23 library controller + 2 hubs + 1 service

---

## ملاحظات للـ Re-implementation في OAM

1. **الاعتماد على Nafath**: المصادقة الأساسية محصورة في Nafath 2FA - يجب الحفاظ على التوافق
2. **SignalR Hubs**: التطبيق يعتمد على NotificationHub و ChatHub - يجب نسخ المنطق
3. **Firebase Integration**: الإشعارات على جميع التطبيقات - يجب تحديد محرك بديل إذا أردنا
4. **Noon Payments**: معالجة الدفع عبر Noon - قد نحتاج واجهة موحدة للبوابات المختلفة
5. **Data Migration**: OffersMigrationService يربط بـ API قديم - قد يحتاج تحديث
6. **Multi-storage Support**: Google Cloud و Alibaba OSS - يجب الحفاظ على كلاهما
7. **Attribution Tracking**: تتبع الحملات التسويقية مهم - يجب نسخ المنطق
8. **Error Reporting**: تقارير الأخطاء عبر SMTP - يجب إعادة تقييم الأداة (Sentry/DataDog)

---

**آخر تحديث**: 2026-04-18  
**الإصدار**: Legacy v1.x  
**الحالة**: Active (قيد الاستخدام)
