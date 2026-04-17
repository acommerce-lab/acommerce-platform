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

`scripts/verify-runtime.sh` يعتمد على Playwright الذي يحتاج تنزيل
متصفّح Chromium من `cdn.playwright.dev`. هذا الـ CDN محجوب بـ 403
في صندوق Claude Web (`Host not in allowlist`). كذلك `chromium-browser`
في Ubuntu 24.04 تحوَّل إلى snap transitional لا يُكمل التثبيت داخل الحاوية.

**البديل الحالي** داخل هذه البيئة هو فحص HTTP spot-check يدوي:

```bash
# شغّل الخدمة + الواجهة في الخلفية
cd Apps/Ashare.V2/Customer/Backend/Ashare.V2.Api && \
  dotnet run --no-build --urls http://localhost:5600 > /tmp/v2-api.log 2>&1 &
cd Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web && \
  dotnet run --no-build --urls http://localhost:5900 > /tmp/v2-web.log 2>&1 &

# انتظر الإقلاع
until curl -sf http://localhost:5600/home/view >/dev/null; do sleep 1; done
until curl -sf http://localhost:5900/             >/dev/null; do sleep 1; done

# تحقق من الـ envelope + تسلسل الـ CSS + التشغيل
curl -s http://localhost:5600/home/view | python3 -m json.tool | head -20
curl -s http://localhost:5900/ | grep -c 'class="acs-page acm-home"'
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5900/_content/ACommerce.Widgets/widgets.css
```

هذا يثبت عمل الـ end-to-end (API → envelope → Web → الـ CSS الذريّ)
لكنّه **لا يغني** عن Layer 6 الذي يفحص computed styles والمحاذاة
والتداخل بـ Playwright. عند توفّر بيئة غير ساندبوكس (مضيف محلي أو CI)،
شغّل:

```bash
cd scripts && npm install && npx playwright install chromium
./scripts/verify-runtime.sh     # أو: TARGET_URLS=http://localhost:5900/ node verify-runtime.mjs
```
