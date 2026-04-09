تقييم سريع: مدى تغطية الخدمات الخلفية لمتطلبات تطبيق Rukkab Rider

فيما يلي مقارنة عملية بين ميزات تطبيق الركاب (Rukkab Rider) وما هو متوفر حالياً في المستودع من خدمات ومكتبات. الهدف: توضيح الفجوات والأولويات اللازمة لجعل التطبيق يعمل كخدمة خلفية حقيقية.

- Authentication (JWT, Users)
  - الحالة: متوفّر (Present)
  - ملاحظات: مكتبات المصادقة موجودة (`ACommerce.Authentication.*`) وتُسجَّل في `Program.cs` عبر امتدادات. مطلوب ضبط إعدادات JWT في `appsettings` عند التشغيل.

- Profiles (User addresses, contact points)
  - الحالة: متوفّر (Present)
  - ملاحظات: `ACommerce.Profiles` يحتوي Controllers وواجهات DI؛ تأكد من إضافة ApplicationPart لعرض المسارات في Swagger.

- Orders / Ride booking lifecycle (create order, accept, in-progress, complete)
  - الحالة: جزئي (Partial)
  - ملاحظات: مكتبة `ACommerce.Orders` توفر بنية الطلبات العامة، لكن سير عمل الركوب (ride lifecycle) قد يحتاج تراكيب/أحداث مخصصة (domain events) وربط قوي مع Notifications وMessaging.

- Notifications (push, in-app) + SignalR
  - الحالة: متوفّر (Present)
  - ملاحظات: أنظمة النوتيفيكيشن موجودة مع قنوات InApp وMessaging وتهيئة SignalR — جاهزة لرفع إشعارات السائق والراكب.

- Chats (in-ride chat)
  - الحالة: متوفّر (Present)
  - ملاحظات: مكتبة الدردشة موجودة وتُشغّل عبر SignalR/Hubs.

- Locations & Maps (geocoding, reverse geocode, autocomplete, nearby)
  - الحالة: متوفّر (Present, dev-only features included)
  - ملاحظات: تمت إضافة مشروع `Other/ACommerce.Locations` و`ACommerce.Locations.Api` مع واجهات وتجميعة in-memory للتطوير. يحتاج الانتقال إلى PostGIS أو مزوّد خارجي في الإنتاج.

- Driver matching / dispatching (nearest driver search, ETA estimate)
  - الحالة: غير مكتمل (Not implemented / design only)
  - ملاحظات: يوجد أساس للموقع والبحث القريب، لكن منطق المطابقة الزمني/المكاني (matching) غير موجود؛ نحتاج خدمة مخصصة تستخدم spatial index (PostGIS) + subscription to driver locations (realtime).

- Payments & Billing
  - الحالة: متوفّر (Present)
  - ملاحظات: موفّرة عبر مكتبات Payments المتعدّدة، تحتاج ربط خطوات الـ checkout ضمن سير الرحلة.

- File storage (driver images, receipts)
  - الحالة: متوفّر (Present)
  - ملاحظات: Local storage adapters + cloud storage providers متاحة في `ACommerce.Files.Storage.*`.

- Metrics, telemetry, logging
  - الحالة: متوفّر (Present)
  - ملاحظات: تسجيلات Serilog موجودة في المشاريع، يمكن ربط بها telemetry/metrics لاحقاً.

أهم الفجوات (أولوية التنفيذ)
- تنفيذ خدمة مطابقة السائقين (Driver Matching) مع دعم spatial indexing واشتراك مواقع السائقين — أولوية عالية.
- ربط مزوّد geocoding/places خارجي أو PostGIS لتغطية دقّة وقابلية التوسع — أولوية متوسطة-عالية.
- ضمان اختبارات تكاملية (smoke tests) لــ Rider+Driver وواجهات Swagger لبيئة التطوير — أولوية متوسطة.

ماذا عدلنا / ماذا شغّلنا حتى الآن
- أضفنا مشروع `ACommerce.Locations` و`ACommerce.Locations.Api` (نماذج وواجهات + controller) مع تنفيذ in-memory لتسهيل التطوير.
- سجّلنا مكتبة المواقع عبر امتداد `AddACommerceLocations()` في خدمات Rider وDriver.
- أصلحت عدة مشاكل في ملفات المشروع (*.csproj*) ومراجع الحزم حتى تُبنى المشاريع بنجاح محلياً.
- شغّلتُ الخدمتين محلياً لأجل الفحص:
  - Rider API على http://127.0.0.1:5101
  - Driver API على http://127.0.0.1:5102

كيفية تشغيل الخدمات محلياً (وصول سريع إلى Swagger)
1) بناء الحل (مرة واحدة أو بعد تغييرات كبيرة):

```bash
dotnet build ACommerce.Libraries.sln
```

2) تشغيل Rider على منفذ مخصّص (مثال 5101) دون إعادة بناء إذا رغبت:

```bash
ASPNETCORE_URLS=http://127.0.0.1:5101 dotnet run --project Apps/Rukkab/Rider/Rider.Api --no-build
```

3) تشغيل Driver على منفذ مختلف (مثال 5102):

```bash
ASPNETCORE_URLS=http://127.0.0.1:5102 dotnet run --project Apps/Rukkab/Driver/Driver.Api --no-build
```

4) فتح واجهة Swagger في المتصفح:
- Rider: http://127.0.0.1:5101/swagger
- Driver: http://127.0.0.1:5102/swagger

5) فحص سريع لملف swagger.json:

```bash
curl -s http://127.0.0.1:5101/swagger/v1/swagger.json | jq '.paths | keys' -r
```

المشاكل المعروفة الآن (يجب إصلاحها أو مراعاتها)
- CS1061 في القوالب: هناك خطأ تجميعي متعلق بـ `NotificationItem.Timestamp` في `Templates/ACommerce.Templates.Customer/Pages/NotificationsPage.razor`.
  - سبب محتمل: النموذج `NotificationItem` لا يحتوي خاصية `Timestamp` أو اسم الخاصية مختلف (`CreatedAt`، `Time`، ...).
  - حل مقترح سريع: فتح تعريف `NotificationItem` وتحديث القالب لاستخدام الخاصية الصحيحة أو إضافة `Timestamp` كمّحوّل/خاصية.
- بعض المشاريع الأخرى بها أخطاء مستقلة (Firebase enum ambiguity، Razor template mismatches، وAndroid SDK configuration للمشاريع الـ MAUI) — هذه غير مرتبطة بتعديلات Locations مباشرة لكن تؤثر على بناء كامل الحل.

خطوات مقترحة قصيرة المدى (التالي)
1) إصلاح `NotificationItem.Timestamp` (أولوية: عالية نسبياً لأنّه يكسر بناء القالب):
   - أبحث عن تعريف `NotificationItem`، حدّث القالب أو النوع.
2) إضافة امتداد dev لإدراج مزوّد الذاكرة المؤقتة للمواقع (مثلاً `AddACommerceLocationsInMemory`) مع seeder بسيط لعرض بيانات اختبارية في Swagger (اختياري، مفيد للـ demos).
3) تنفيذ اختبار تدخّل (smoke) بسيط يتحقّق من أن `/swagger/v1/swagger.json` يعيد HTTP 200 لـ Rider وDriver.

ملاحظات للمتابعة
- لقد سجّلت هذه البنود في قائمة المهام (todo list) في المستودع: إصلاح `NotificationItem.Timestamp` وتحديث الدليل.
- هل تفضّل أن أبدأ فوراً بإصلاح خطأ `NotificationItem.Timestamp` أم أضيف مزوّد الذاكرة المؤقتة للـ Locations أولاً؟
