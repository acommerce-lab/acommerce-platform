# ACommerce.Libraries - فهرس التوثيق

## نظرة عامة
توثيق شامل لمكتبات ACommerce.Libraries - حل متكامل للتجارة الإلكترونية متعدد البائعين.

**الإحصائيات:**
- إجمالي المشاريع: 100
- الملفات الموثقة: 48+
- نسبة التغطية: ~48%

---

## الفئات الرئيسية

### Core (الأساسيات) ✅ 80%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| SharedKernel.Abstractions | الكيانات والواجهات الأساسية | [📄](core/01-SharedKernel.Abstractions.md) |
| SharedKernel.CQRS | Commands, Queries, Handlers | [📄](core/02-SharedKernel.CQRS.md) |
| SharedKernel.Infrastructure.EFCore | Repository و DbContext | [📄](core/03-SharedKernel.Infrastructure.EFCore.md) |
| Configuration | الإعدادات (Store, Vendor) | [📄](core/04-Configuration.md) |
| Notifications.Abstractions | الإشعارات متعددة القنوات | [📄](notifications/01-Notifications.Abstractions.md) |
| Realtime.Abstractions | SignalR Abstractions | ⏳ |
| Chats.Abstractions | نظام المحادثات | ⏳ |

### Authentication (المصادقة) ✅ 83%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Authentication.Abstractions | IAuthenticationProvider | [📄](authentication/01-Authentication.Abstractions.md) |
| Authentication.JWT | JWT Token Provider | [📄](authentication/02-Authentication.JWT.md) |
| Authentication.TwoFactor.Nafath | مصادقة نفاذ | [📄](authentication/03-Authentication.TwoFactor.Nafath.md) |
| Authentication.TwoFactor.Abstractions | تجريدات 2FA | [📄](authentication/04-Authentication.TwoFactor.Abstractions.md) |
| Authentication.Users.Abstractions | إدارة المستخدمين | ⏳ |

### Catalog (الكاتالوج) ✅ 80%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Catalog.Products | إدارة المنتجات | [📄](catalog/01-Catalog.Products.md) |
| Catalog.Categories | التصنيفات | [📄](catalog/02-Catalog.Categories.md) |
| Catalog.Attributes | السمات الديناميكية | [📄](catalog/03-Catalog.Attributes.md) |
| Catalog.Listings | عروض المنتجات (Multi-Vendor) | [📄](catalog/04-Catalog.Listings.md) |
| Catalog.Listings.Api | API للعروض | ⏳ |

### Sales (المبيعات) ✅ 67%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Cart | سلة التسوق | [📄](sales/01-Cart.md) |
| Orders | الطلبات | [📄](sales/02-Orders.md) |
| Orders.Api | API للطلبات | ⏳ |

### Payments (المدفوعات) ✅ 75%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Payments.Abstractions | IPaymentProvider | [📄](payments/01-Payments.Abstractions.md) |
| Payments.Moyasar | بوابة Moyasar | [📄](payments/02-Payments.Moyasar.md) |
| Payments.Api | API للمدفوعات | [📄](payments/03-Payments.Api.md) |

### Shipping (الشحن) ✅ 75%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Shipping.Abstractions | IShippingProvider | [📄](shipping/01-Shipping.Abstractions.md) |
| Shipping.Mock | مزود وهمي للاختبار | [📄](shipping/02-Shipping.Mock.md) |
| Shipping.Api | API للشحن | [📄](shipping/03-Shipping.Api.md) |

### Files (الملفات) ✅ 100%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Files.Abstractions | IStorageProvider, IImageProcessor | [📄](files/01-Files.Abstractions.md) |
| Files.Storage.Local | تخزين محلي | [📄](files/02-Files.Storage.Local.md) |
| Files.ImageProcessing | معالجة الصور (ImageSharp) | [📄](files/03-Files.ImageProcessing.md) |

### Notifications (الإشعارات) ✅ 33%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Notifications.Abstractions | INotificationChannel | [📄](notifications/01-Notifications.Abstractions.md) |
| Notifications.Channels.* | قنوات الإشعارات | [📄](notifications/02-Notifications.Channels.md) |
| Notifications.Messaging | تكامل Message Bus | [📄](notifications/03-Notifications.Messaging.md) |
| Notifications.Core | منطق الإشعارات | ⏳ |
| Notifications.Recipients | المستلمين | ⏳ |

### Modules (الوحدات) ✅ 100%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Reviews | نظام التقييمات العام | [📄](modules/01-Reviews.md) |
| Localization | دعم متعدد اللغات | [📄](modules/02-Localization.md) |

### Clients (SDKs العميل) ⏳ 13%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Client.Core | IApiClient, Interceptors | [📄](clients/01-Client.Core.md) |
| Client.* (15 SDK) | SDKs للخدمات المختلفة | [📄](clients/02-Client-SDKs-Overview.md) |

### Infrastructure (البنية التحتية) ⏳ 50%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| ServiceRegistry.* | Service Discovery | ⏳ |

### Identity (الهوية) ⏳ 50%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Profiles | الملفات الشخصية | [📄](identity/01-Profiles.md) |
| Profiles.Api | API للبروفايلات | ⏳ |

### Messaging (الرسائل) ⏳ 50%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Messaging.Abstractions | IMessageBus, Pub/Sub | ⏳ |
| Messaging.InMemory | للاختبار | ⏳ |
| Messaging.SignalR | للاتصال اللحظي | ⏳ |

### Marketplace (السوق) ⏳ 50%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Vendors | إدارة البائعين | [📄](marketplace/01-Vendors.md) |
| Vendors.Api | API للبائعين | ⏳ |

### Locations (المواقع الجغرافية) ✅ 100%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| Locations.Abstractions | الكيانات والعقود | [📄](locations/01-Locations.Abstractions.md) |
| Locations | التنفيذ وEF Core | [📄](locations/02-Locations.md) |
| Locations.Api | API Controllers | [📄](locations/03-Locations.Api.md) |

### AspNetCore ⏳ 40%
| المكتبة | الوصف | التوثيق |
|---------|-------|---------|
| SharedKernel.AspNetCore | BaseCrudController | [📄](aspnetcore/01-SharedKernel.AspNetCore.md) |
| Authentication.AspNetCore | Controllers للمصادقة | [📄](aspnetcore/02-Authentication.AspNetCore.md) |
| Files.AspNetCore | Controllers للملفات | ⏳ |
| Authentication.AspNetCore.Swagger | Swagger Auth | ⏳ |
| Authentication.AspNetCore.NafathWH | Nafath Webhook | ⏳ |

---

## الأنماط المستخدمة

1. **CQRS** - فصل القراءة عن الكتابة
2. **Repository Pattern** - تجريد الوصول للبيانات
3. **Provider Pattern** - تبديل التنفيذات
4. **Soft Delete** - حذف منطقي
5. **Domain Events** - أحداث المجال
6. **SmartSearch** - بحث متقدم مع فلترة

---

## الأدلة والمقالات

### الأدلة
| الدليل | الوصف |
|--------|-------|
| [Microservices Backend Guide](../guides/Microservices-Backend-Guide.md) | دليل بناء Microservices |
| [Monolith Backend Guide](../guides/Monolith-Backend-Guide.md) | دليل بناء Monolith |
| [MAUI Blazor Guide](../guides/MAUI-Blazor-Guide.md) | دليل تطبيقات الموبايل |

### المقالات
| المقالة | الوصف |
|---------|-------|
| [Best Practices](../articles/Best-Practices.md) | أفضل الممارسات |

---

## كيفية الاستخدام

1. اختر المكتبات المناسبة لمشروعك
2. أضفها كـ NuGet packages
3. سجل الخدمات في `Program.cs`
4. استخدم الـ Base Controllers أو أنشئ خاصتك

---

## ملاحظات
- جميع المكتبات تعتمد على .NET 9.0
- التوثيق باللغة العربية
- يمكن استخدام هذا التوثيق كمصدر بيانات للـ AI Agent
