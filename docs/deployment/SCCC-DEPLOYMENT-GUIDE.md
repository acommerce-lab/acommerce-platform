# دليل النشر على Alibaba Cloud SCCC السعودية

## نظرة عامة

SCCC (Saudi Cloud Computing Company) هي شراكة بين STC و Alibaba Cloud تقدم خدمات سحابية محلية في السعودية مع مراكز بيانات في الرياض.

**الروابط المهمة:**
- بوابة SCCC: https://www.sccc.sa
- Console: https://sccc.console.aliyun.com

---

## الخطوة 1: إنشاء قاعدة البيانات SQL Server RDS

### 1.1 الدخول للـ Console

1. اذهب إلى https://www.sccc.sa
2. سجل الدخول بحسابك
3. من القائمة اختر: **Products > ApsaraDB RDS**

### 1.2 إنشاء RDS Instance

اضغط **Create Instance** واختر:

| الإعداد | القيمة |
|---------|--------|
| **Region** | Saudi Arabia (Riyadh) |
| **Database Engine** | Microsoft SQL Server |
| **Version** | 2019 أو 2022 |
| **Edition** | High-availability (للإنتاج) |
| **Storage Type** | ESSD |
| **Instance Type** | حسب احتياجك (ابدأ بـ 2 vCPU, 4GB RAM) |
| **Network Type** | VPC |
| **Billing** | Pay-As-You-Go (للتجربة) |

### 1.3 إعدادات الشبكة

بعد إنشاء الـ Instance:

1. **إضافة Whitelist:**
   - Instance Details > Data Security > Whitelist Settings
   - اضغط Add Whitelist Group
   - أضف IP الخاص بك أو `0.0.0.0/0` للتجربة (غير آمن للإنتاج)

2. **تفعيل Public Endpoint:**
   - Instance Details > Basic Information
   - اضغط Apply for Public Endpoint
   - انتظر حتى يظهر الـ Public Endpoint

### 1.4 إنشاء Database و Account

1. **إنشاء حساب:**
   - Instance Details > Accounts > Create Account
   - Account Name: `ashare_admin`
   - Account Type: Privileged Account
   - Password: كلمة مرور قوية

2. **إنشاء قاعدة البيانات:**
   - Instance Details > Databases > Create Database
   - Database Name: `AshareDb`
   - Character Set: `SQL_Latin1_General_CP1_CI_AS`

### 1.5 Connection String

```
Server=rm-xxxxx.sqlserver.rds.aliyuncs.com,3433;Database=AshareDb;User Id=ashare_admin;Password=YourPassword;TrustServerCertificate=true;Encrypt=true
```

---

## الخطوة 2: إنشاء OSS Bucket للملفات

### 2.1 إنشاء Bucket

1. من Console اختر: **Products > Object Storage Service**
2. اضغط **Create Bucket**

| الإعداد | القيمة |
|---------|--------|
| **Bucket Name** | `ashare-files` |
| **Region** | Saudi Arabia (Riyadh) |
| **Storage Class** | Standard |
| **ACL** | Private |
| **Versioning** | Disabled (أو Enabled للنسخ الاحتياطي) |

### 2.2 إنشاء Access Keys

1. اذهب إلى **RAM Console**
2. اختر **Users > Create User**
3. اسم المستخدم: `ashare-oss-user`
4. فعّل **Programmatic Access**
5. اضغط Create
6. **احفظ Access Key ID و Access Key Secret** (لن تظهر مرة أخرى!)

### 2.3 منح صلاحيات OSS

1. اختر المستخدم الجديد
2. اضغط **Add Permissions**
3. أضف: `AliyunOSSFullAccess`

### 2.4 إعدادات التطبيق

أضف في `appsettings.Production.json`:

```json
{
  "Files": {
    "Storage": {
      "AliyunOSS": {
        "AccessKeyId": "YOUR_ACCESS_KEY_ID",
        "AccessKeySecret": "YOUR_ACCESS_KEY_SECRET",
        "Endpoint": "https://oss-me-central-1.aliyuncs.com",
        "Region": "me-central-1",
        "BucketName": "ashare-files",
        "UseHttps": true,
        "UseV4Signature": true
      }
    }
  }
}
```

---

## الخطوة 3: إنشاء ECS Instance للاستضافة

### 3.1 إنشاء ECS

1. من Console: **Products > Elastic Compute Service**
2. اضغط **Create Instance**

| الإعداد | القيمة |
|---------|--------|
| **Region** | Saudi Arabia (Riyadh) |
| **Instance Type** | ecs.c6.large (2 vCPU, 4GB) كحد أدنى |
| **Image** | Ubuntu 22.04 LTS |
| **System Disk** | 40GB ESSD |
| **Network** | نفس VPC الخاص بالـ RDS |
| **Public IP** | Assign Public IP |
| **Security Group** | أنشئ جديد |

### 3.2 إعداد Security Group

افتح المنافذ التالية:

| Port | Protocol | Source | الغرض |
|------|----------|--------|--------|
| 22 | TCP | Your IP | SSH |
| 80 | TCP | 0.0.0.0/0 | HTTP |
| 443 | TCP | 0.0.0.0/0 | HTTPS |
| 5000 | TCP | 0.0.0.0/0 | API (مؤقت للتجربة) |

### 3.3 تثبيت .NET على ECS

اتصل بالـ ECS عبر SSH:

```bash
# تحديث النظام
sudo apt update && sudo apt upgrade -y

# تثبيت .NET 9.0
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt update
sudo apt install -y dotnet-sdk-9.0 dotnet-runtime-9.0

# التحقق
dotnet --version
```

---

## الخطوة 4: نشر التطبيق

### 4.1 بناء التطبيق محلياً

```bash
# Backend API
cd Apps/Ashare.Api
dotnet publish -c Release -o ./publish

# Frontend Web
cd ../Ashare.Web
dotnet publish -c Release -o ./publish
```

### 4.2 رفع الملفات للـ ECS

```bash
# استخدم SCP لنقل الملفات
scp -r ./publish/* root@YOUR_ECS_IP:/var/www/ashare-api/
```

### 4.3 إعداد Systemd Service

على الـ ECS، أنشئ:

**/etc/systemd/system/ashare-api.service:**
```ini
[Unit]
Description=Ashare API
After=network.target

[Service]
WorkingDirectory=/var/www/ashare-api
ExecStart=/usr/bin/dotnet Ashare.Api.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000

[Install]
WantedBy=multi-user.target
```

**/etc/systemd/system/ashare-web.service:**
```ini
[Unit]
Description=Ashare Web
After=network.target

[Service]
WorkingDirectory=/var/www/ashare-web
ExecStart=/usr/bin/dotnet Ashare.Web.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5001

[Install]
WantedBy=multi-user.target
```

### 4.4 تشغيل الخدمات

```bash
sudo systemctl daemon-reload
sudo systemctl enable ashare-api ashare-web
sudo systemctl start ashare-api ashare-web
sudo systemctl status ashare-api ashare-web
```

---

## الخطوة 5: إعداد Nginx (Reverse Proxy)

### 5.1 تثبيت Nginx

```bash
sudo apt install nginx -y
```

### 5.2 إعداد Nginx

**/etc/nginx/sites-available/ashare:**
```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass http://localhost:5001;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /api/ {
        proxy_pass http://localhost:5000/api/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### 5.3 تفعيل الموقع

```bash
sudo ln -s /etc/nginx/sites-available/ashare /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## الخطوة 6: إعداد SSL (اختياري لكن مهم)

```bash
# تثبيت Certbot
sudo apt install certbot python3-certbot-nginx -y

# الحصول على شهادة SSL
sudo certbot --nginx -d your-domain.com
```

---

## استخدام المكتبة الجديدة للملفات

### في Program.cs:

```csharp
// بدلاً من Local Storage
// services.AddLocalFileStorage(builder.Configuration);

// استخدم Aliyun OSS
using ACommerce.Files.Storage.AliyunOSS.Extensions;

services.AddAliyunOSSFileStorage(builder.Configuration);
```

### في appsettings.json:

```json
{
  "Files": {
    "Storage": {
      "AliyunOSS": {
        "AccessKeyId": "YOUR_ACCESS_KEY_ID",
        "AccessKeySecret": "YOUR_ACCESS_KEY_SECRET",
        "Endpoint": "https://oss-me-central-1.aliyuncs.com",
        "Region": "me-central-1",
        "BucketName": "ashare-files",
        "UseHttps": true,
        "UseV4Signature": true,
        "UseDirectoryStructure": true,
        "DirectoryFormat": "yyyy/MM/dd",
        "SignedUrlExpirationMinutes": 60
      }
    }
  }
}
```

---

## ملاحظات مهمة

1. **أمان المفاتيح:** لا تضع Access Keys في الكود! استخدم Environment Variables أو Secrets Manager
2. **VPC:** تأكد أن ECS و RDS في نفس VPC للاتصال الداخلي
3. **Whitelist:** في الإنتاج، حدد IPs معينة بدلاً من `0.0.0.0/0`
4. **النسخ الاحتياطي:** فعّل Auto Backup في RDS
5. **المراقبة:** استخدم CloudMonitor لمراقبة الخدمات

---

## التسعير التقريبي (شهرياً)

| الخدمة | المواصفات | التكلفة التقريبية |
|--------|-----------|-------------------|
| ECS | 2 vCPU, 4GB RAM | ~$40 |
| RDS SQL Server | 2 vCPU, 4GB RAM | ~$100 |
| OSS | 50GB Storage | ~$5 |
| SLB | Basic | ~$15 |
| **المجموع** | | **~$160/شهر** |

*الأسعار تقريبية وتختلف حسب الاستخدام*
