# خطة إعادة هيكلة مستودع ACommerce

## 📊 جرد المشاريع الحالي

### إحصائيات عامة
- **إجمالي المشاريع**: 114 مشروع
- **تاريخ الجرد**: December 14, 2025

### توزيع المشاريع حسب المجلدات الحالية

| المجلد | عدد المشاريع | الوصف |
|--------|-------------|-------|
| Other | 24 | مشاريع متنوعة (Accounting, Reviews, Subscriptions...) |
| Clients | 19 | مكتبات SDK للعميل |
| Authentication | 9 | مكتبات المصادقة |
| Core | 8 | المكتبات الأساسية (SharedKernel, CQRS...) |
| Files | 5 | إدارة الملفات |
| AspNetCore | 5 | تكامل ASP.NET Core |
| Apps | 5 | التطبيقات (Ashare.Web, Ashare.App...) |
| Payments | 4 | بوابات الدفع |
| Marketplace | 4 | السوق متعدد البائعين |
| Infrastructure | 4 | البنية التحتية |
| Shipping | 3 | الشحن |
| Sales | 3 | المبيعات والطلبات |
| Examples | 3 | أمثلة توضيحية |
| Templates | 2 | قوالب UI |
| Modules | 2 | وحدات |
| Identity | 2 | الهوية |
| Catalog | 2 | الكتالوج |
| Root (Messaging, Profiles...) | 8 | مشاريع في الجذر |

### المشاريع الأكثر استخداماً (حسب عدد المراجع)

| المشروع | عدد المراجع | الموقع المقترح |
|---------|------------|----------------|
| SharedKernel.AspNetCore | 19 | libs/backend |
| Client.Core | 18 | libs/frontend |
| SharedKernel.CQRS | 16 | libs/backend |
| SharedKernel.Abstractions | 14 | libs/backend |
| Messaging.Abstractions | 6 | libs/backend |
| ServiceRegistry.Client | 5 | libs/frontend |
| Vendors | 5 | libs/backend |

---

## 🎯 الهيكل المستهدف

### خيار 1: التنظيم حسب الوظيفة (Domain-Based) ⭐ مُوصى به
```
ACommerce/
├── libs/
│   ├── backend/
│   │   ├── core/                    ← SharedKernel, CQRS, Configuration
│   │   ├── auth/                    ← JWT, TwoFactor, Identity, Nafath
│   │   ├── catalog/                 ← Products, Categories, Listings, Attributes
│   │   ├── sales/                   ← Cart, Orders, Payments
│   │   ├── marketplace/             ← Vendors, Commissions, Reviews
│   │   ├── shipping/                ← Providers, Tracking
│   │   ├── messaging/               ← SignalR, Notifications, Chats
│   │   ├── files/                   ← Storage, Media
│   │   └── integration/             ← ASP.NET Core, Swagger
│   │
│   └── frontend/
│       ├── core/                    ← DynamicHttpClient, Interceptors
│       ├── clients/                 ← Auth, Cart, Orders, Products...
│       ├── realtime/                ← SignalR Client, Hubs
│       └── discovery/               ← ServiceRegistry
│
```

### خيار 2: التنظيم حسب الطبقة (Layer-Based)
```
ACommerce/
├── libs/
│   ├── backend/
│   │   ├── abstractions/            ← كل الـ Interfaces والـ Contracts
│   │   ├── domain/                  ← Entities, Value Objects, Business Logic
│   │   ├── infrastructure/          ← EF Core, External Services
│   │   ├── api/                     ← Controllers, Endpoints
│   │   └── aspnetcore/              ← Middleware, Filters
│   │
│   └── frontend/
│       ├── abstractions/            ← Interfaces
│       ├── http/                    ← HTTP Clients
│       ├── realtime/                ← SignalR
│       └── storage/                 ← Local Storage, Cache
│
```

### المقارنة

| المعيار | حسب الوظيفة | حسب الطبقة |
|---------|-------------|------------|
| سهولة الفهم | ✅ أسهل | ⚠️ يحتاج خبرة |
| الاستقلالية | ✅ كل وحدة مستقلة | ❌ تبعيات متشابكة |
| إعادة الاستخدام | ✅ سهل | ⚠️ متوسط |
| التوسع المستقبلي | ✅ سهل | ⚠️ يحتاج تخطيط |
| CLI/AI Agent | ✅ مناسب جداً | ⚠️ معقد |

**التوصية**: خيار 1 (حسب الوظيفة) لأنه:
- أسهل للـ CLI في توليد الأكواد
- أسهل للـ AI Agent في فهم البنية
- كل مجال عمل مستقل ويمكن نشره كـ NuGet منفصل

---

### الهيكل التفصيلي المُوصى به

```
ACommerce/
├── libs/
│   ├── backend/
│   │   ├── core/
│   │   │   ├── ACommerce.SharedKernel.Abstractions
│   │   │   ├── ACommerce.SharedKernel.CQRS
│   │   │   ├── ACommerce.SharedKernel.Infrastructure
│   │   │   └── ACommerce.Configuration
│   │   │
│   │   ├── auth/
│   │   │   ├── ACommerce.Authentication.Abstractions
│   │   │   ├── ACommerce.Authentication.JWT
│   │   │   ├── ACommerce.Authentication.TwoFactor.*
│   │   │   ├── ACommerce.Identity.*
│   │   │   └── ACommerce.Profiles.*
│   │   │
│   │   ├── catalog/
│   │   │   ├── ACommerce.Catalog.Listings
│   │   │   └── ACommerce.Catalog.Listings.Api
│   │   │
│   │   ├── sales/
│   │   │   ├── ACommerce.Cart
│   │   │   ├── ACommerce.Orders
│   │   │   └── ACommerce.Payments.*
│   │   │
│   │   ├── marketplace/
│   │   │   ├── ACommerce.Vendors
│   │   │   ├── ACommerce.Commissions
│   │   │   └── ACommerce.Reviews
│   │   │
│   │   ├── messaging/
│   │   │   ├── ACommerce.Messaging.*
│   │   │   ├── ACommerce.Notifications.*
│   │   │   ├── ACommerce.Chats.*
│   │   │   └── ACommerce.Realtime.*
│   │   │
│   │   ├── files/
│   │   │   └── ACommerce.Files.*
│   │   │
│   │   ├── shipping/
│   │   │   └── ACommerce.Shipping.*
│   │   │
│   │   └── integration/
│   │       └── ACommerce.*.AspNetCore
│   │
│   └── frontend/
│       ├── core/
│       │   └── ACommerce.Client.Core
│       │
│       ├── clients/
│       │   ├── ACommerce.Client.Auth
│       │   ├── ACommerce.Client.Cart
│       │   ├── ACommerce.Client.Orders
│       │   ├── ACommerce.Client.Products
│       │   ├── ACommerce.Client.Categories
│       │   ├── ACommerce.Client.Profiles
│       │   ├── ACommerce.Client.Payments
│       │   ├── ACommerce.Client.Vendors
│       │   ├── ACommerce.Client.Notifications
│       │   ├── ACommerce.Client.Chats
│       │   ├── ACommerce.Client.Files
│       │   └── ACommerce.Client.*
│       │
│       ├── realtime/
│       │   └── ACommerce.Client.Realtime
│       │
│       └── discovery/
│           └── ACommerce.ServiceRegistry.Client
│
├── templates/
│   ├── core/                        ← Base Template (Analytics, Localization, Auth Pages)
│   │   ├── services/
│   │   ├── components/
│   │   └── layouts/
│   │
│   ├── real-estate/                 ← قالب عقاري (Ashare-derived)
│   ├── fashion/                     ← قالب أزياء
│   ├── electronics/                 ← قالب إلكترونيات
│   └── services/                    ← قالب خدمات
│
└── apps/
    ├── ashare/                      ← تطبيق عشير (Web + Mobile)
    │   ├── web/
    │   ├── mobile/
    │   └── shared/
    │
    └── examples/
        ├── eshop-api/
        └── eshop-maui/
```

---

## 📋 خطة التنفيذ المرحلية

### المرحلة 0: الجرد والتوثيق ✅
- [x] إحصاء المشاريع
- [x] تحليل التبعيات
- [x] توثيق الخطة

### المرحلة 1: إعادة تنظيم المجلدات
**الهدف**: نقل المشاريع للهيكل الجديد بدون تغيير Namespaces

1. إنشاء مجلدات `libs/backend`, `libs/frontend`
2. نقل مشاريع Core للموقع الجديد
3. تحديث ملف Solution
4. اختبار البناء

**المخاطر**: منخفضة (لا تغيير في الكود)

### المرحلة 2: استخراج Core Template
**الهدف**: فصل القالب النواة عن القالب المتخصص

1. إنشاء `templates/core`
2. نقل الخدمات العامة (Analytics, Localization)
3. نقل المكونات الأساسية
4. إنشاء `templates/real-estate` من Ashare

**المخاطر**: متوسطة

### المرحلة 3: تنظيف Apps
**الهدف**: فصل Ashare عن Examples

1. نقل Ashare إلى `apps/ashare`
2. نقل Examples إلى `apps/examples`
3. تحديث المراجع

**المخاطر**: منخفضة

### المرحلة 4: إنشاء CLI
**الهدف**: أداة سطر أوامر لإنشاء تطبيقات جديدة

```bash
acommerce new my-store --template real-estate
acommerce add payments --provider moyasar
acommerce generate page ProductDetails
```

### المرحلة 5: AI Agent (مستقبلي)
**الهدف**: وكيل ذكي لإنشاء التطبيقات بأوامر لغوية

---

## 🔍 تفاصيل التبعيات

### Backend Core (يجب نقلها أولاً)
```
Core/
├── ACommerce.SharedKernel.Abstractions    ← الأساس (14 مرجع)
├── ACommerce.SharedKernel.CQRS            ← CQRS Pattern (16 مرجع)
├── ACommerce.SharedKernel.Infrastructure.EFCores
├── ACommerce.Configuration
├── ACommerce.Realtime.Abstractions
├── ACommerce.Notifications.Abstractions
├── ACommerce.Chats.Abstractions
└── ACommerce.Locations.Abstractions
```

### Frontend Core (يجب نقلها ثانياً)
```
Clients/
├── ACommerce.Client.Core                  ← الأساس (18 مرجع)
├── ACommerce.Client.Auth
├── ACommerce.Client.Cart
├── ACommerce.Client.Orders
├── ACommerce.Client.Products
├── ACommerce.Client.Categories
├── ACommerce.Client.Profiles
├── ACommerce.Client.Payments
├── ACommerce.Client.Vendors
├── ACommerce.Client.Notifications
├── ACommerce.Client.Chats
├── ACommerce.Client.Realtime
├── ACommerce.Client.Files
├── ACommerce.Client.Locations
├── ACommerce.Client.ContactPoints
├── ACommerce.Client.ProductListings
├── ACommerce.Client.Nafath
├── ACommerce.Client.Shipping
└── ACommerce.Client.Subscriptions
```

---

## 📁 المشاريع في الجذر (تحتاج نقل)

هذه المشاريع موجودة في الجذر ويجب نقلها:

| المشروع | الموقع المقترح |
|---------|---------------|
| ACommerce.Messaging.* | libs/backend/messaging |
| ACommerce.Profiles.* | libs/backend/identity |
| ACommerce.Notifications.Messaging | libs/backend/messaging |
| ACommerce.Authentication.Messaging | libs/backend/messaging |
| ACommerce.Authentication.TwoFactor.SessionStore.* | libs/backend/authentication |

---

## ⚠️ ملاحظات مهمة

1. **لا تغيير في Namespaces في المرحلة 1** - فقط نقل ملفات
2. **اختبار البناء بعد كل خطوة**
3. **الحفاظ على ملف Solution واحد** مع Solution Filters
4. **توثيق كل تغيير** في هذا الملف

---

## 📅 سجل التقدم

| التاريخ | المرحلة | الحالة | ملاحظات |
|---------|--------|--------|---------|
| 2025-12-14 | 0 | ✅ مكتمل | جرد 114 مشروع |
| 2025-12-14 | 1 | ✅ مكتمل | نقل 98 مشروع + تحديث Solution + البناء ناجح |
| | 2 | ⏳ قيد الانتظار | استخراج Core Template |
| | 3 | ⏳ قيد الانتظار | تنظيف Apps |
