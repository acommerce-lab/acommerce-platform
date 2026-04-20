# دليل تكامل الإنتاج — ACommerce Platform

دليل تعميم التكاملات من Ashare V2 على أي تطبيق جديد في المنصة.

## المكتبات المتاحة

جميع التكاملات جاهزة في `libs/backend/`. أضفها كـ `ProjectReference` فقط.

| المكتبة | المسار | ماذا تضيف |
|---------|--------|-----------|
| `ACommerce.Authentication.TwoFactor.Providers.Nafath` | `libs/backend/auth/` | نفاذ — التحقق بالهوية الوطنية |
| `ACommerce.Payments.Providers.Noon` | `libs/backend/sales/` | بوابة دفع Noon Pay |
| `ACommerce.Files.Storage.AliyunOSS` | `libs/backend/files/` | رفع الملفات — Alibaba Cloud OSS |
| `ACommerce.Files.Storage.Local` | `libs/backend/files/` | رفع الملفات — محلي (للتطوير) |
| `ACommerce.Notification.Providers.Firebase` | `libs/backend/messaging/` | إشعارات Push — Firebase FCM |
| `ACommerce.Notification.Providers.Email` | `libs/backend/messaging/` | إشعارات بريد إلكتروني |
| `ACommerce.Notification.Providers.InApp` | `libs/backend/messaging/` | إشعارات داخل التطبيق |
| `ACommerce.Realtime.Providers.InMemory` | `libs/backend/messaging/` | قناة Realtime للـ InApp |
| `ACommerce.SharedKernel.Infrastructure.EFCores` | `libs/backend/core/` | قاعدة البيانات (EF Core) |

---

## نموذج csproj

```xml
<ItemGroup>
  <!-- EF Core (يشمل SQLite + SQL Server + PostgreSQL) -->
  <ProjectReference Include="path/to/libs/backend/core/ACommerce.SharedKernel.Infrastructure.EFCores/..." />

  <!-- Auth -->
  <ProjectReference Include="path/to/libs/backend/auth/ACommerce.Authentication.Operations/..." />
  <ProjectReference Include="path/to/libs/backend/auth/ACommerce.Authentication.TwoFactor.Providers.Nafath/..." />

  <!-- Payments -->
  <ProjectReference Include="path/to/libs/backend/sales/ACommerce.Payments.Operations/..." />
  <ProjectReference Include="path/to/libs/backend/sales/ACommerce.Payments.Providers.Noon/..." />

  <!-- Files -->
  <ProjectReference Include="path/to/libs/backend/files/ACommerce.Files.Operations/..." />
  <ProjectReference Include="path/to/libs/backend/files/ACommerce.Files.Storage.AliyunOSS/..." />
  <ProjectReference Include="path/to/libs/backend/files/ACommerce.Files.Storage.Local/..." />

  <!-- Notifications -->
  <ProjectReference Include="path/to/libs/backend/messaging/ACommerce.Notification.Operations/..." />
  <ProjectReference Include="path/to/libs/backend/messaging/ACommerce.Notification.Providers.Firebase/..." />
  <ProjectReference Include="path/to/libs/backend/messaging/ACommerce.Notification.Providers.Email/..." />
  <ProjectReference Include="path/to/libs/backend/messaging/ACommerce.Notification.Providers.InApp/..." />
  <ProjectReference Include="path/to/libs/backend/messaging/ACommerce.Realtime.Operations/..." />
  <ProjectReference Include="path/to/libs/backend/messaging/ACommerce.Realtime.Providers.InMemory/..." />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
  <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
  <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
  <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
</ItemGroup>
```

---

## نموذج Program.cs — تسجيل المزودين

```csharp
using ACommerce.Authentication.TwoFactor.Providers.Nafath.Extensions;
using ACommerce.Files.Storage.AliyunOSS.Extensions;
using ACommerce.Files.Storage.Local.Extensions;
using ACommerce.Notification.Providers.Email.Extensions;
using ACommerce.Notification.Providers.Firebase.Extensions;
using ACommerce.Notification.Providers.InApp.Extensions;
using ACommerce.Payments.Providers.Noon.Extensions;
using ACommerce.Realtime.Providers.InMemory.Extensions;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// ── Database ──────────────────────────────────────────────────────────────
var connStr = cfg.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connStr))
{
    if (env.IsDevelopment() || connStr.StartsWith("Data Source="))
        builder.Services.AddACommerceSQLite(connStr);
    else
        builder.Services.AddACommerceSqlServer(connStr);
}

// ── JWT Bearer ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(cfg["JWT:SecretKey"]!)),
            ValidateIssuer           = true,
            ValidIssuer              = cfg["JWT:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = cfg["JWT:Audience"],
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromMinutes(2),
        };
    });
builder.Services.AddAuthorization();

// ── Nafath 2FA ────────────────────────────────────────────────────────────
// القسم: Authentication:TwoFactor:Nafath
if (!string.IsNullOrEmpty(cfg["Authentication:TwoFactor:Nafath:ApiKey"]))
    builder.Services.AddNafathTwoFactor(cfg);

// ── Noon Pay ──────────────────────────────────────────────────────────────
// القسم: Payments:Noon
if (!string.IsNullOrEmpty(cfg["Payments:Noon:ApplicationKey"]))
    builder.Services.AddNoonPaymentGateway(cfg);

// ── File Storage ──────────────────────────────────────────────────────────
// القسم: Files:Storage:Provider = "AliyunOSS" | "Local"
if ((cfg["Files:Storage:Provider"] ?? "Local").Equals("AliyunOSS", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddAliyunOSSFileStorage(cfg);
else
    builder.Services.AddLocalFileStorage(cfg);

// ── Notifications ─────────────────────────────────────────────────────────
builder.Services.AddInMemoryRealtimeTransport();
builder.Services.AddInAppNotificationChannel();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON")
    ?? cfg["Notifications:Firebase:ServiceAccountKeyJson"]))
    builder.Services.AddFirebaseNotificationChannel(cfg);

if (!string.IsNullOrEmpty(cfg["Email:Smtp:Host"]))
    builder.Services.AddEmailNotifications(cfg);

// ── Middleware (الترتيب مهم) ──────────────────────────────────────────────
app.UseAuthentication();
app.UseAuthorization();
```

---

## قسم أسماء الإعدادات (Section Names)

| المزود | القسم في appsettings |
|--------|----------------------|
| Nafath | `Authentication:TwoFactor:Nafath` |
| Noon   | `Payments:Noon` |
| Firebase | `Notifications:Firebase` |
| AliyunOSS | `Files:Storage:AliyunOSS` |
| Local Storage | `Files:Storage:Local` |
| Email SMTP | `Email` |
| JWT | `JWT` |
| DB | `ConnectionStrings:DefaultConnection` |

---

## ملفات appsettings — البنية المقترحة

### appsettings.json (مشترك — بدون أسرار)
```json
{
  "HostSettings": { "BaseUrl": "...", "WebBaseUrl": "..." },
  "JWT": { "Issuer": "...", "Audience": "..." },
  "Authentication": { "TwoFactor": { "Nafath": { "BaseUrl": "...", "Mode": "Live" } } },
  "Payments": { "Noon": { "ApplicationIdentifier": "...", "BusinessIdentifier": "...", "Region": "SA" } },
  "Files": { "Storage": { "AliyunOSS": { "Endpoint": "...", "BucketName": "..." } } },
  "Notifications": { "Firebase": { "ProjectId": "..." } }
}
```

### appsettings.Development.json (تطوير — قيم وهمية)
```json
{
  "ConnectionStrings": { "DefaultConnection": "Data Source=data/myapp-dev.db" },
  "JWT": { "Issuer": "http://localhost:5000", "Audience": "myapp-api-dev" },
  "Payments": { "Noon": { "IsSandbox": true, "Environment": "Test" } },
  "Files": { "Storage": { "Provider": "Local" } }
}
```

### appsettings.Production.json (إنتاج — بدون أسرار)
```json
{
  "Payments": { "Noon": { "IsSandbox": false, "Environment": "Live" } },
  "Files": { "Storage": { "Provider": "AliyunOSS" } },
  "Cors": { "AllowedOrigins": ["https://myapp.sa"] }
}
```

---

## متغيرات البيئة على الخادم

### إنشاء ملف البيئة (Linux / systemd)
```bash
sudo mkdir -p /etc/myapp
sudo nano /etc/myapp/myapp-api.env
sudo chmod 600 /etc/myapp/myapp-api.env
```

### محتوى ملف البيئة
```env
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5600
ConnectionStrings__DefaultConnection=Server=...;Database=...;User Id=...;Password=...
JWT__SecretKey=$(openssl rand -base64 48)
JWT__Issuer=https://api.myapp.sa
JWT__Audience=myapp-api
Authentication__TwoFactor__Nafath__ApiKey=YOUR_API_KEY
Payments__Noon__ApplicationKey=YOUR_NOON_KEY
Files__Storage__Provider=AliyunOSS
Files__Storage__AliyunOSS__AccessKeyId=YOUR_KEY_ID
Files__Storage__AliyunOSS__AccessKeySecret=YOUR_SECRET
FIREBASE_SERVICE_ACCOUNT_JSON={"type":"service_account",...}
Email__Smtp__Host=smtp.gmail.com
Email__Smtp__Port=587
Email__Smtp__Username=YOUR_EMAIL
Email__Smtp__Password=YOUR_APP_PASSWORD
```

### ملف systemd service
```ini
[Unit]
Description=MyApp API
After=network.target

[Service]
WorkingDirectory=/var/www/myapp/api
ExecStart=/usr/bin/dotnet /var/www/myapp/api/MyApp.Api.dll
Restart=always
RestartSec=10
EnvironmentFile=/etc/myapp/myapp-api.env
User=www-data
Group=www-data
SyslogIdentifier=myapp-api

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable myapp-api
sudo systemctl start myapp-api
sudo journalctl -u myapp-api -f
```

### توليد JWT Secret آمن
```bash
openssl rand -base64 48
# نسخ الناتج إلى JWT__SecretKey في ملف البيئة
```

---

## مراحل نقل التطبيق للإنتاج (Checklist)

### المرحلة 1 — التكاملات (✅ منجزة في Ashare V2)
- [x] إضافة project references من libs/backend
- [x] تسجيل المزودين في Program.cs مع منطق dev/prod
- [x] إنشاء appsettings.json + Development + Production
- [x] إنشاء .env.Development + .env.Production

### المرحلة 2 — قاعدة البيانات الحقيقية
- [ ] إنشاء Migration الأولى: `dotnet ef migrations add InitialCreate`
- [ ] تطبيق Migration: `dotnet ef database update`
- [ ] استبدال controllers لاستخدام DbContext بدلاً من البيانات المبدئية

### المرحلة 3 — الإنتاج
- [ ] ضبط متغيرات البيئة على الخادم
- [ ] تشغيل خدمة systemd
- [ ] إعداد Nginx reverse proxy
- [ ] تفعيل HTTPS عبر Let's Encrypt
- [ ] اختبار `/healthz` endpoint

---

## ملاحظات مهمة

1. **لا ترفع `.env.*` إلى git** — أضفهما في `.gitignore`
2. **مفتاح JWT يجب أن يبقى سرياً** — تغييره يلغي جميع الجلسات الحالية
3. **نفاذ Mode=Test** في التطوير — لا يتحقق فعلاً من الهوية الوطنية
4. **Noon Sandbox** في التطوير — بطاقات اختبار فقط، لا خصم حقيقي
5. **Firebase اختياري في التطوير** — الإشعارات InApp تعمل بدونه
