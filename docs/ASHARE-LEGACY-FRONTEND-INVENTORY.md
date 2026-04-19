# Ashare Legacy Frontend — Inventory

**المصدر**: `/tmp/ACommerce.Libraries/Apps/Ashare.{Web,App,Shared,Admin}/`
**الغرض**: جرد شامل لكلّ الواجهات في المستودع القديم — مرجعيّة لخطّة الترحيل إلى V2.

---

## 1. البنية العليا

4 مشاريع منفصلة يتشاركون Library مشترك (`Ashare.Shared`):

| المشروع | النوع | يستضيف الصفحات؟ | تعليق |
|---|---|---|---|
| `Ashare.Web` | Blazor Server (WebHost) | لا — shell فقط | يستهلك صفحات `Ashare.Shared` عبر `AddAdditionalAssemblies` |
| `Ashare.App` | MAUI Blazor Hybrid | لا — shell فقط | نفس الصفحات المشتركة + أكواد خاصّة بالمنصّة (Firebase / Skia / MediaPicker) |
| `Ashare.Shared` | Razor Class Library | **نعم — 25 صفحة + 2 shared + MainLayout** | الوحدة الحقيقيّة للواجهة |
| `Ashare.Admin` | Blazor Server (Syncfusion) | نعم — 13 صفحة مستقلّة | واجهة إدارة معزولة |

**النتيجة**: الترحيل = نقل `Ashare.Shared` كاملاً + نقل ميزات shell (Web / MAUI) + إعادة كتابة `Ashare.Admin` داخل V2.

---

## 2. Ashare.Shared — مكتبة الصفحات المشتركة

### 2.1 الصفحات (25 صفحة، 12,249 سطر إجماليّاً)

**الزوّار / الدخول**:
| الصفحة | المسار | الحجم | الوصف | خدمات مُحقَنة |
|---|---|---:|---|---|
| `Home.razor` | `/` | 234 | الرئيسيّة — hero + فئات + عرض | `AshareApiService`, `IAppNavigationService` |
| `Search.razor` | `/search` | 349 | بحث + فلاتر | `AshareApiService` |
| `Explore.razor` | `/explore` | 467 | تصفّح مُصنَّف | `AshareApiService`, Categories |
| `SpaceDetails.razor` | `/space/{SpaceId:guid}` | 1,455 | تفاصيل المساحة + معرض + محادثة + حجز (أكبر صفحة) | `AshareApiService`, `ChatsClient`, `ProfilesClient`, `TokenManager`, `JS` |
| `Login.razor` | `/login` | 667 | مصادقة عبر نفاذ + realtime | `NafathClient`, `AuthClient`, `RealtimeClient`, `TokenManager`, `GuestModeService`, `AppLifecycle`, `Theme`, `L` |
| `Auth/Register.razor` | `/register` | 16 | stub (لا تسجيل يدويّ — نفاذ فقط) | — |
| `Language.razor` | `/language` | 165 | مُبدِّل اللغة | `LocalizationService` |
| `LegalPageView.razor` | `/legal/{Key}` | 220 | عرض محتوى قانونيّ ديناميكيّ | `LegalPagesClient` |

**العميل المُسجَّل**:
| الصفحة | المسار | الحجم | الوصف | خدمات مُحقَنة |
|---|---|---:|---|---|
| `Favorites.razor` | `/favorites` | 122 | مفضّلة المستخدم | `AshareApiService` |
| `Profile.razor` | `/profile` | 483 | ملفّ المستخدم + القوائم الخاصّة به | `ProfilesClient`, `TokenManager`, `AuthClient` |
| `Auth/ProfileEdit.razor` | `/profile/edit` | 382 | تحرير الملفّ (الصورة/الاسم/الوصف) | `ProfilesClient`, `FilesClient` |
| `Notifications.razor` | `/notifications` | 170 | قائمة الإشعارات | `NotificationsClient` |

**المحادثات**:
| الصفحة | المسار | الحجم | الوصف | خدمات مُحقَنة |
|---|---|---:|---|---|
| `Chat/Chats.razor` | `/chats` | 227 | قائمة المحادثات | `ChatsClient` |
| `Chat/ChatRoom.razor` | `/chat/{ChatId:guid}` | 455 | غرفة محادثة + SignalR | `ChatsClient`, `RealtimeClient` |

**الحجوزات (عميل)**:
| الصفحة | المسار | الحجم | الوصف |
|---|---|---:|---|
| `Bookings.razor` | `/bookings` | 413 | حجوزات المستخدم (active/past/cancelled) |
| `BookingCreate.razor` | `/book/{SpaceId:guid}` | 965 | إنشاء حجز: اختيار مدّة + تسعير + أحكام + دفع |
| `BookingDetails.razor` | `/booking/{BookingId:guid}` | 988 | تفاصيل حجز + actions (إلغاء/دفع/تقييم) |

**المستضيف (Host)**:
| الصفحة | المسار | الحجم | الوصف |
|---|---|---:|---|
| `CreateListing.razor` | `/create-listing`, `/host/add` | 585 | إنشاء إعلان مساحة (wizard) |
| `Host/MySpaces.razor` | `/host/spaces` | 599 | قائمة مساحات المستضيف |
| `Host/SubscriptionPlans.razor` | `/host/plans` | 296 | عرض خطط الاشتراك |
| `Host/SubscriptionCheckout.razor` | `/host/subscribe/{PlanSlug}` | 828 | شراء خطّة + دفع |
| `Host/SubscriptionDashboard.razor` | `/host/subscription/dashboard` + `/host/subscription` | 352 | حالة الاشتراك الحاليّ |
| `Host/PaymentCallback.razor` | `/host/payment/callback` | 326 | صفحة العودة من بوّابة الدفع |

**الشكاوى**:
| الصفحة | المسار | الحجم | الوصف |
|---|---|---:|---|
| `Complaints.razor` | `/complaints` | 759 | قائمة شكاوى/تذاكر |
| `ComplaintDetails.razor` | `/complaints/{Id:guid}` | 726 | تفاصيل شكوى + ردود + مُرفقات |

### 2.2 مكوّنات مشتركة

- `Components/Layout/MainLayout.razor` — التخطيط الرئيسيّ (header + bottom nav + body)
- `Components/Shared/LazyImage.razor` — صورة كسولة مع placeholder
- `Components/Shared/PaymentWebView.razor` — WebView مُدمج لـ 3DS/OTP (مفيد على MAUI)

### 2.3 الخدمات (Services/)

| الخدمة | الواجهة | الدور |
|---|---|---|
| `AshareApiService` | concrete | واجهة موحّدة للـ Catalog/Products/Listings/Orders/Bookings/Subscriptions/Payments مع memory cache |
| `ApiConfiguration` / `IApiConfiguration` | contract | إعداد عناوين الـ API |
| `AshareSubscriptionPlans` | concrete | بيانات خطط الاشتراك الثابتة (fallback) |
| `BaseNavigationService` | abstract | أساس للتنقّل (Web/MAUI يطبّقانه) |
| `IAppLifecycleService` | contract | دورة حياة التطبيق (foreground/background) |
| `IAppVersionService` | contract | التحقّق من نسخة التطبيق |
| `ITrackingConsentService` | contract | موافقة المستخدم على التتبّع التسويقيّ |
| `LocalizationService` | concrete | i18n — قاموس `L["Key"]` |
| `PendingListingService` | concrete | إعلانات مُعلّقة قيد الإنشاء (wizard state) |
| `SpaceDataService` | concrete | بيانات المساحات المؤقّتة (قبل ربط API حقيقيّ) |
| `ThemeService` | concrete | وضع داكن/فاتح (`IsDarkMode`) |
| `TokenStorageService` | concrete | حفظ/تحميل الـ JWT |
| `VersionCheckService` | concrete | مقارنة النسخة مع أحدث منشورة |

### 2.4 النماذج والملحقات

- `Models/PaymentModels.cs` — DTOs للمدفوعات
- `Extensions/ServiceCollectionExtensions.cs` — `AddAshareServices()` يسجّل كلّ ما سبق

### 2.5 wwwroot

- `images/ashare-logo.png`
- CSS مخصّص (`ashare-*` classes متصلة بفئات `ac-*` من widgets)
- خطوط عربيّة (Tajawal / IBM Plex Arabic عادةً في مثل هذه المشاريع)

---

## 3. Ashare.Web — Blazor Server Shell

**Program.cs** (69 سطراً) يسجّل:
- `IStorageService → BrowserStorageService` (localStorage عبر JS)
- `ITokenStorage → StorageBackedTokenStorage`
- `TokenManager` (scoped) + `ScopedTokenProvider`
- `AddAshareClients(apiBaseUrl, options)` — يضخّ الـ token في كلّ الـ API clients
- `AddAshareServices()` (من Shared)
- `ThemeService`, `GuestModeService`, `SpaceDataService`
- `IAppNavigationService → WebNavigationService`
- `ITimezoneService → BrowserTimezoneService`
- `IPaymentService → WebPaymentService`
- `IAppVersionService → WebAppVersionService`
- `AddACommerceAnalytics(configuration)`
- `AddLocalizationValidation()` (يتحقّق من اكتمال الترجمات عند dev)
- `AddAdditionalAssemblies(typeof(Ashare.Shared._Imports).Assembly)` — هنا يتم سحب الصفحات.

**Services محلّية (5)**: `BrowserStorageService`, `ScopedTokenProvider`, `WebAppVersionService`, `WebNavigationService`, `WebPaymentService`.

---

## 4. Ashare.App — MAUI Blazor Hybrid

**MauiProgram.cs** (162 سطراً) — يضيف على Shared:

- `AddACommerceCustomerTemplate(options)` — نظام ألوان كامل (Primary/Secondary/Success/Error/Warning/Info + Background/Surface) + RTL + Light mode.
- Firebase (Android via `CrossFirebase.Initialize`, iOS في `AppDelegate`).
- `PaymentWebViewHandler` مخصّص لأندرويد (3DS/OTP).
- Services محلّية:
  - `MauiStorageService` (يحلّ محل BrowserStorage)
  - `TokenManager` (Singleton على MAUI)
  - `MauiDeviceInfoProvider` + `TrackingConsentService` + `TrackingConsentInterceptor`
  - `MauiMediaPickerService` (camera/gallery)
  - `SkiaImageCompressionService`
  - `PushNotificationService` (Firebase Cloud Messaging)
  - `AttributionCaptureService` (deep-link tracking)
  - `DeviceTimezoneService`, `MauiPaymentService`, `AppVersionService`, `AppLifecycleService`
  - `AppNavigationService` (Maui-flavoured)
- `RealtimeClientOptions.BypassSslValidation` (DEBUG).
- `AddMockAnalytics()` بدل التحليلات الحقيقيّة.
- `HttpClientHandler` يتجاهل شهادات SSL في DEBUG.
- Firebase initialization + fonts (OpenSans).

**Pages محلّية**: `PaymentPage.xaml` (غلاف native للدفع).

**Platforms/**: Android + iOS + Windows handlers.

**Resources/Styles/**: `Colors.xaml`, `Styles.xaml`.

---

## 5. Ashare.Admin — واجهة الإدارة

**Program.cs** (73 سطراً):
- Syncfusion license + `AddSyncfusionBlazor()`
- `AddBlazoredLocalStorage()` (بدل JS مخصّص)
- `AddAuthorizationCore()` + `AdminAuthStateProvider`
- `AdminTokenProvider` (Singleton)
- `AddACommerceStaticClient(apiBaseUrl, EnableAuthentication = true)` — SDK الثابت (StaticClient) بدل AshareClients
- `DataProtection` إلى FileSystem (`keys/`)
- `AdminApiService`, `MarketingAnalyticsService`

**الصفحات (13)**: `AdminUsers`, `Dashboard`, `Listings`, `Login`, `Marketing`, `Notifications`, `Orders`, `Reports`, `Roles`, `Settings`, `Subscriptions`, `UserDetails`, `Users`, `Versions`.

**Layouts**: `MainLayout`, `EmptyLayout` (للـ Login).

**مكوّنات خاصّة**: `RedirectToLogin.razor`, `Routes.razor`.

**Services (5)**: `AdminApiService`, `AdminAuthStateProvider`, `AdminTokenProvider`, `AuthService`, `MarketingAnalyticsService`.

---

## 6. اعتماديات عرضيّة

- SDK عملاء ACommerce (client-generated):
  `AuthClient`, `NafathClient`, `RealtimeClient`, `TokenManager`, `ProfilesClient`, `CategoriesClient`, `CategoryAttributesClient`, `ProductsClient`, `ProductListingsClient`, `OrdersClient`, `BookingsClient`, `SubscriptionClient`, `PaymentsClient`, `ChatsClient`, `FilesClient`, `NotificationsClient`, `LegalPagesClient`, `VersionsClient`, `ComplaintsClient`.
- `AshareApiService` غلاف موحّد مع memory cache فوقها.
- الأنماط: `bi bi-*` من Bootstrap Icons، فئات `ashare-*` خاصّة، فئات `ac-*` من widgets.
- المصادقة: JWT + Nafath 2FA.
- Realtime: SignalR hubs (`/hubs/chat`, `/hubs/notifications`, `/hubs/messaging`).
- تعدّد اللغات: `ILocalizationService L` + `L["Key"]` + RTL تلقائيّ.

---

## 7. توزيع الحجم (سطور)

| الطبقة | سطور | ملاحظة |
|---|---:|---|
| `Ashare.Shared` Pages | ~12,249 | 25 صفحة |
| `Ashare.Shared` Services/Models/Extensions | ~1,200 تقديريّاً | 13+ خدمة |
| `Ashare.Web` shell | ~400 | 5 خدمات |
| `Ashare.App` MAUI | ~1,500 تقديريّاً | Firebase/Skia/Handlers |
| `Ashare.Admin` | ~2,500 تقديريّاً | 13 صفحة + 5 خدمات + Syncfusion |
| **الإجماليّ التقديريّ** | **~18,000 سطر** | — |

---

## 8. نقاط تتطلّب تكيّفاً مع V2 / OAM

1. **`AshareApiService` يجب أن يختفي** — القانون 4 في `ASHARE-PAGE-MIGRATION.md:18`: "لا `AshareApiService` ولا خدمات أخرى — كل مُتغيّر حالة = `Entry.Create(...)`".
2. **فئات `ashare-*` و`bi bi-*`** يجب أن تتحوّل إلى `Ac*` widgets.
3. **`NafathClient` المُستخدَم عبر الواجهة** يجب أن يصبح عمليّات `auth.nafath.initiate` / `auth.nafath.complete` عبر `ClientOpEngine` + `HttpDispatcher`.
4. **Syncfusion في Admin** غير متوافق مع قانون الـ widgets — يجب استبدال جميع مكوّناته بـ `Ac*` + `acx-admin-*` templates.
5. **MAUI shell** قد لا يُنقَل حاليّاً (V2 يستهدف Server أوّلاً) — يُوَثَّق كـ Phase مستقلّة.
6. **SignalR hubs** ترتبط بـ `RealtimeClient` القديم — تحتاج لتركيب مُعترض/عقد Provider للرسائل المباشرة.
7. **DynamicAttributes** (للمساحات) لم تظهر صراحةً في الصفحات القديمة، لكنّ المنهجيّة الحاليّة (`DYNAMIC-ATTRIBUTES.md`) تتطلّبها — Template + Snapshot.
