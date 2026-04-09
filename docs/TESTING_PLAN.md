# خطة اختبار المكتبات المُعاد هيكلتها

## الهدف
التأكد من أن المكتبات في `libs/` تعمل بشكل صحيح مع تطبيقات الإنتاج قبل حذف المجلدات القديمة.

---

## المرحلة 1: إصلاح عرض Visual Studio

### المشكلة
Visual Studio يعرض Solution Folders القديمة (AspNetCore, Authentication, Core...) بدلاً من الهيكل الجديد (libs/backend/*, libs/frontend/*).

### الحل
1. إنشاء Solution Folders جديدة تطابق الهيكل الفعلي
2. إعادة تعيين المشاريع للمجلدات الجديدة
3. حذف Solution Folders القديمة

### الهيكل المطلوب في Visual Studio:
```
Solution 'ACommerce.Libraries'
├── libs/
│   ├── backend/
│   │   ├── core/           (5 مشاريع)
│   │   ├── auth/           (15 مشروع)
│   │   ├── catalog/        (11 مشروع)
│   │   ├── sales/          (7 مشاريع)
│   │   ├── marketplace/    (4 مشاريع)
│   │   ├── messaging/      (17 مشروع)
│   │   ├── files/          (5 مشاريع)
│   │   ├── shipping/       (3 مشاريع)
│   │   ├── integration/    (11 مشروع)
│   │   └── other/          (4 مشاريع)
│   └── frontend/
│       ├── core/           (1 مشروع)
│       ├── clients/        (18 مشروع)
│       ├── realtime/       (1 مشروع)
│       └── discovery/      (1 مشروع)
├── apps/
│   ├── Ashare.Web
│   ├── Ashare.App
│   └── Examples/
├── templates/
└── Solution Items/
```

---

## المرحلة 2: اختبار Ashare.Web (تطبيق الويب)

### خطوات الاختبار:
1. **تنظيف وإعادة البناء**:
   ```bash
   dotnet clean Apps/Ashare.Web
   dotnet restore Apps/Ashare.Web
   dotnet build Apps/Ashare.Web
   ```

2. **تشغيل التطبيق**:
   ```bash
   dotnet run --project Apps/Ashare.Web
   ```

3. **اختبار الوظائف الأساسية**:
   - [ ] تسجيل الدخول (Authentication)
   - [ ] عرض الإعلانات (Catalog/Listings)
   - [ ] البحث والتصفية
   - [ ] صفحة البائع (Vendors)
   - [ ] الدردشة (Chats)
   - [ ] الإشعارات (Notifications)
   - [ ] رفع الصور (Files)

4. **فحص السجلات**:
   - التأكد من عدم وجود أخطاء في DI Registration
   - التأكد من تحميل جميع الخدمات

---

## المرحلة 3: اختبار Ashare.App (تطبيق الموبايل)

### خطوات الاختبار:
1. **تحديث MAUI Workload**:
   ```bash
   dotnet workload update
   dotnet workload install maui-android
   ```

2. **تنظيف وإعادة البناء**:
   ```bash
   dotnet clean Apps/Ashare.App
   dotnet restore Apps/Ashare.App
   dotnet build Apps/Ashare.App -f net9.0-android
   ```

3. **النشر على المحاكي**:
   ```bash
   dotnet build Apps/Ashare.App -f net9.0-android -t:Run
   ```

4. **اختبار الوظائف الأساسية**:
   - [ ] تسجيل الدخول
   - [ ] عرض الإعلانات
   - [ ] البحث
   - [ ] الدردشة
   - [ ] الإشعارات
   - [ ] رفع الصور

---

## المرحلة 4: معايير النجاح

### قبل حذف المجلدات القديمة:
1. ✅ البناء ناجح لجميع المشاريع
2. ✅ Ashare.Web يعمل بدون أخطاء
3. ✅ Ashare.App يعمل على المحاكي
4. ✅ جميع الاختبارات الوظيفية ناجحة
5. ✅ لا توجد أخطاء في السجلات

### بعد حذف المجلدات القديمة:
1. إعادة البناء والتأكد من النجاح
2. مراقبة التطبيقات لمدة 48 ساعة

---

## المرحلة 5: الحذف الآمن

### المجلدات المراد حذفها:
```
Core/
Authentication/
Clients/
Infrastructure/
Files/
Marketplace/
Sales/
Payments/
Shipping/
Catalog/
Identity/
Other/
ACommerce.Messaging.*/
ACommerce.Profiles.*/
ACommerce.Authentication.Messaging/
ACommerce.Authentication.TwoFactor.SessionStore.*/
ACommerce.Notifications.Messaging/
```

### خطوات الحذف:
1. إنشاء نسخة احتياطية (git branch)
2. حذف المجلدات
3. إعادة البناء
4. تشغيل الاختبارات
5. مراقبة الإنتاج

---

## سجل التقدم

| التاريخ | الخطوة | الحالة | ملاحظات |
|---------|--------|--------|---------|
| 2025-12-14 | نقل المشاريع | ✅ مكتمل | 98 مشروع |
| | إصلاح Solution | ⏳ قيد العمل | |
| | اختبار Ashare.Web | ⏳ قيد الانتظار | |
| | اختبار Ashare.App | ⏳ قيد الانتظار | |
| | حذف المجلدات | ⏳ قيد الانتظار | |
