# تثبيت .NET 10 في بيئة الجلسة

الأداة مطلوبة لبناء كل مشاريع المستودع (`TargetFramework=net10.0` في كل
`.csproj`). بيئات Claude Web تأتي بدونها افتراضياً، وهذا الملف يختصر
إجراءات التثبيت ليُنفَّذ في بداية كل جلسة جديدة.

## التثبيت السريع (Ubuntu 24.04)

```bash
apt-get update \
  -o Dir::Etc::sourcelist="sources.list" \
  -o Dir::Etc::sourceparts="-" \
  -o APT::Get::List-Cleanup="0"

apt-get install -y --fix-missing dotnet-sdk-10.0
```

> `Dir::Etc::sourcelist` يمنع قراءة PPA الإضافيّة (deadsnakes, ondrej/php)
> التي تفشل عادةً بـ 403 في بيئات ساندبوكس. المصدر الرسمي لـ Ubuntu وحده
> يكفي لجلب `dotnet-sdk-10.0`.

## التحقّق

```bash
dotnet --version       # يجب أن يطبع 10.0.x
dotnet --list-sdks     # يجب أن يُظهر /usr/lib/dotnet/sdk
```

## بناء اختباري سريع

```bash
dotnet build libs/backend/core/ACommerce.OperationEngine/ACommerce.OperationEngine.csproj --nologo -v q
```

يجب أن يطبع `Build succeeded. 0 Warning(s) 0 Error(s)`.

## ملاحظات

- لا تستخدم `https://dot.net/v1/dotnet-install.sh` — محظور (403) على
  بعض البيئات.
- `apt-get update` بلا الخيارات أعلاه يفشل بسبب PPAs غير موقَّعة ويعيد
  رمز خطأ يُوقف الـ pipeline.
- الإصدار المُثبَّت حالياً في بيئات Ubuntu 24.04 الرسمية هو **10.0.106**
  (SDK) + **10.0.6** (Runtime).

## Playwright / Layer 6 runtime verification

`scripts/verify-runtime.sh` يعتمد على Playwright. في صندوق Claude Web
يُحجَب `cdn.playwright.dev` بـ 403 (Host not in allowlist)، وحزمة
`chromium-browser` في Ubuntu 24.04 صارت snap transitional لا تُثبَّت.

**الحل**: نزّل Chrome for Testing من `storage.googleapis.com`
(مسموح به) ووجّه `verify-runtime.mjs` إليه عبر `CHROME_EXEC_PATH`.

### تثبيت Chrome for Testing

```bash
mkdir -p /opt/browsers && cd /opt/browsers
curl -sSL -o chrome-linux64.zip \
  "https://storage.googleapis.com/chrome-for-testing-public/131.0.6778.204/linux64/chrome-linux64.zip"
unzip -q chrome-linux64.zip
chmod +x chrome-linux64/chrome
/opt/browsers/chrome-linux64/chrome --version    # يطبع: Google Chrome for Testing 131.x
```

> إصدارات بديلة على: `https://googlechromelabs.github.io/chrome-for-testing/`.

### تبعيّات npm

```bash
cd scripts && npm install --silent    # يجلب حزمة playwright فقط (بدون تنزيل متصفح)
```

### تشغيل الفحوص على Ashare.V2

```bash
# 1. شغّل الـ API والواجهة
cd Apps/Ashare.V2/Customer/Backend/Ashare.V2.Api && \
  dotnet run --no-build --urls http://localhost:5600 > /tmp/v2-api.log 2>&1 &
cd Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web && \
  dotnet run --no-build --urls http://localhost:5900 > /tmp/v2-web.log 2>&1 &

# 2. انتظر الإقلاع
until curl -sf http://localhost:5600/home/view >/dev/null; do sleep 1; done
until curl -sf http://localhost:5900/             >/dev/null; do sleep 1; done

# 3. شغّل Layer 6
cd scripts && \
  CHROME_EXEC_PATH=/opt/browsers/chrome-linux64/chrome \
  TARGET_URLS="http://localhost:5900/" \
  node verify-runtime.mjs
```

ينبغي أن تطبع: `[1/1] loaded  0 viol  http://localhost:5900/`. التقرير
الكامل في `runtime-report.json` (JSON). أيّ قيمة `viol > 0` تكشف مشكلة
runtime (تباين ألوان، تخطيط، مواضع، etc.) — عالجها قبل الاستمرار.

### لماذا Chrome for Testing وليس Playwright default؟

- Playwright يُنزّل من `cdn.playwright.dev` المحجوب.
- `storage.googleapis.com/chrome-for-testing-public/` مسموح به في الصندوق.
- ‍`chromium.launch({ executablePath })` يتجاوز تنزيل Playwright الداخلي
  تماماً — لا حاجة لـ `npx playwright install`.
- Chrome for Testing مبنيٌّ من نفس شجرة Chromium ويتحدّث بـ DevTools
  Protocol الذي يستخدمه Playwright؛ التوافق كامل.
