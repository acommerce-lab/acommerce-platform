# AshareMigrator

أداة ترحيل بيانات عشير من قاعدة SQL Server الإنتاجية القديمة إلى قاعدة SQLite محلية بالصيغة الجديدة (Dynamic Attributes + OAM).

## الغرض

- قراءة البيانات من قاعدة عشير الإنتاجية (SQL Server على Google Cloud SQL).
- تحويلها إلى الكيانات الجديدة (`NewUser`, `NewListing`, ...).
- بناء **لقطة الصفات الديناميكية** (`DynamicAttributesJson`) من `AttributesJson` القديم حسب قالب كل فئة.
- الكتابة إلى ملف SQLite محلي لمقارنة التشغيل مع النسخة القديمة.

## المبدأ: لا حذف بيانات

- كل مفتاح صفات قديم موجود في قالب الفئة الجديدة → يُدمج في اللقطة كحقل كامل المعنى.
- كل مفتاح قديم **غير** موجود في القالب → يُحفظ حرفياً في اللقطة كـ `DynamicAttribute` إضافي بنوع مُستنتَج.
- يعني: إذا أضاف صاحب المصلحة صفات مخصصة لم نتوقعها، لن تضيع.

## الإعداد

1. عدّل `appsettings.json` بسلسلة اتصال SQL Server الصحيحة (كلمة المرور يدوياً).
2. أو استخدم متغيرات بيئة:
   ```bash
   export ASHARE_MIGRATOR_Source="Server=...;Database=...;User Id=sa;Password=...;TrustServerCertificate=True;"
   export ASHARE_MIGRATOR_Target="Data Source=C:/acommerce/data/ashare-migrated.db"
   ```
3. أو مرّرها كوسائط في سطر الأوامر.

## التشغيل

```bash
# ترحيل عادي (idempotent — يتخطى الصفوف الموجودة بنفس Id)
dotnet run --project tools/AshareMigrator

# مع تصفير الهدف قبل الترحيل
dotnet run --project tools/AshareMigrator -- --truncate true

# بدون appsettings.json
dotnet run --project tools/AshareMigrator -- \
  --src "Server=34.166.82.42;Database=AshareDb;User Id=sa;Password=xxx;TrustServerCertificate=True;" \
  --dst "Data Source=./data/ashare-migrated.db"
```

## ترتيب الترحيل

1. **الفئات** (Categories) — مع `AttributeTemplateJson` من `AshareTemplates`.
2. **المستخدمون + الملفات الشخصية** — مع تحديد الدور (`owner`/`customer`) بناءً على وجود Vendor.
3. **العروض** (Listings) — `OwnerId` = `userId` المرتبط بـ Vendor، اللقطة من `AttributesJson` القديم + القالب.
4. **الحجوزات** (Bookings) — `SpaceId→ListingId`, `HostId→OwnerId`, `CustomerId` نص → Guid.
5. **خطط الاشتراك** (Plans).
6. **الاشتراكات** (Subscriptions) — `VendorId` → `UserId`.

## ملاحظات

- المشروع مُكتفٍ ذاتياً: لا يرجع إلى `Ashare.Api` (مشروع Web SDK). قوالب الفئات مُنسَّخة في `Templates/AshareTemplates.cs`. أي تعديل على القوالب الأصلية يجب أن ينعكس هنا.
- المخطط الهدف يُنشَأ عبر `EnsureCreatedAsync` — لا توجد migrations.
- `SqliteSchemaGuard` (في التطبيق الرئيسي) سيعيد بناء الملف إذا اختلف المخطط؛ هنا لا نستخدمه لأن الهدف ليس قاعدة تطبيق.
- سلسلة اتصال SQL Server تُطبع مع إخفاء كلمة المرور.

## بعد الترحيل

وجّه تطبيق `Ashare.Api` المحلي إلى ملف SQLite نفسه (أو انسخه إلى `data/ashare.db`) ثم شغّله وقارن سلوك قراءة/عرض البيانات بين القديم والجديد.
