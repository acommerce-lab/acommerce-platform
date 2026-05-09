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

### تشغيل الفحوص على Ejar (WASM)

Ejar.WebAssembly تطبيق **Blazor WebAssembly** — يبدأ بشاشة "جارٍ تحميل
إيجار…" ثمّ يستبدل DOM عند اكتمال WASM. Layer 6 يحتاج انتظاراً صريحاً
بعد التحميل قبل قياس الـ DOM؛ الإصدار الحاليّ من `verify-runtime.mjs`
يضيف بالفعل `page.waitForFunction(...)` للحالات WASM.

```bash
# 1. الباك (port من appsettings.Development.json)
dotnet run --project Apps/Ejar/Customer/Backend/Ejar.Api \
  --no-build --urls http://localhost:5300 > /tmp/ejar-api.log 2>&1 &

# 2. الواجهة (WASM dev)
dotnet run --project Apps/Ejar/Customer/Frontend/Ejar.WebAssembly \
  --no-build --urls http://localhost:5301 > /tmp/ejar-wasm.log 2>&1 &

# 3. انتظر الإقلاع
until curl -sf http://localhost:5300/version/check >/dev/null; do sleep 1; done
until curl -sf http://localhost:5301/             >/dev/null; do sleep 1; done

# 4. Layer 6 — يحتاج waitForSelector على DOM ما-بعد-WASM
cd scripts && \
  CHROME_EXEC_PATH=/opt/browsers/chrome-linux64/chrome \
  TARGET_URLS="http://localhost:5301/" \
  WAIT_SELECTOR=".acm-mobile-nav,.acm-top-nav" \
  node verify-runtime.mjs
```

`WAIT_SELECTOR` يُجبر Playwright على الانتظار حتى يظهر شريط التنقّل
(آخر شيء يُركَّب بعد إقلاع Blazor). بدونه يقيس الشاشة الانتقاليّة
ويُبلِّغ خطأ كاذب.

### لماذا Chrome for Testing وليس Playwright default؟

- Playwright يُنزّل من `cdn.playwright.dev` المحجوب.
- `storage.googleapis.com/chrome-for-testing-public/` مسموح به في الصندوق.
- ‍`chromium.launch({ executablePath })` يتجاوز تنزيل Playwright الداخلي
  تماماً — لا حاجة لـ `npx playwright install`.
- Chrome for Testing مبنيٌّ من نفس شجرة Chromium ويتحدّث بـ DevTools
  Protocol الذي يستخدمه Playwright؛ التوافق كامل.

---

## مرجع موجز — الطبقات الست + xUnit

طبقات الجودة موزّعة على ست أدوات. الفحوص ١-٥ ثابتة (لا تحتاج تطبيقاً
شغّالاً)؛ السادسة runtime (تتطلّب Chrome for Testing + خادمَين قائمَين).
كلّ فحص يُجيب عن سؤال واحد فقط — لا تتكرر الأسئلة.

| # | الأداة | السؤال | بناء التطبيق مطلوب؟ |
|---|---|---|---|
| 1 | `scripts/verify-page-structure.sh` | راز خام بدون widgets؟ Bootstrap classes؟ ألوان hex inline؟ | لا |
| 2 | `scripts/verify-css.sh` (+ `verify-css.csx`) | كلّ class مستعمل في `.razor` معرَّف في `.css`؟ | لا |
| 3 | `scripts/verify-design-tokens.sh` | كلّ font-size / padding / icon-size على المقياس؟ | لا |
| 4 | `scripts/verify-design-quality.sh` | تطبيق ضمن حدود تنوّع الألوان / المسافات / الخطوط؟ | لا |
| 5 | `scripts/verify-widget-contracts.sh` | كلّ widget يعلن خصائص CSS الإلزاميّة؟ | لا |
| 6 | `scripts/verify-runtime.mjs` (Playwright) | كل عنصر في موضعه؟ تباين WCAG؟ بدون تداخل؟ | **نعم** |

### تشغيل سريع — الكلّ

```bash
# طبقات ١-٥ (ثابتة، تعمل على المستودع كاملاً أو مسار محدَّد)
./scripts/verify-page-structure.sh
./scripts/verify-css.sh
./scripts/verify-design-tokens.sh
./scripts/verify-design-quality.sh        # تقرير فقط — لا تُكسر البناء
./scripts/verify-widget-contracts.sh

# طبقات ١-٥ مع تحديد تطبيق
./scripts/verify-page-structure.sh Apps/Ejar/Customer/Frontend
./scripts/verify-css.sh              Apps/Ejar/Customer/Shared

# الطبقة السادسة — راجع قسم Ejar (WASM) أعلاه
```

كلّ سكربت يُرجع `exit 1` عند الفشل ويطبع الملف + السطر + السبب.
Layer 4 يطبع تقريراً بدون كسر البناء (يقيس مقاييس إجماليّة).

### اختبارات xUnit (Backend Integration Tests)

تطبيق Ejar يحوي مشروع اختبار: `tests/Ejar.Api.Tests` — يستخدم
`Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>` فيرفع
الـ host فعلياً في الذاكرة بدون port حقيقيّ، ويرسل HTTP requests فيتحقّق
من شكل `OperationEnvelope`. SQLite ملف مؤقّت بدل MSSQL.

```bash
# كلّ اختبارات Ejar
dotnet test tests/Ejar.Api.Tests

# تفصيلاً
dotnet test tests/Ejar.Api.Tests -v normal

# بـ filter
dotnet test tests/Ejar.Api.Tests --filter "Both_parties_receive"

# بتغطية كود
dotnet test tests/Ejar.Api.Tests --collect:"XPlat Code Coverage"

# الحلّ كاملاً
dotnet test ACommerce.Platform.sln
```

> **هام لـ xUnit + WASM**: إن أضفت اختباراً يستهلك واجهة WASM، استخدم
> `bUnit` (لاختبار مكوّنات Blazor معزولة) لا `WebApplicationFactory`.
> `WebApplicationFactory` يصلح للـ API فقط — لا يُحاكي WASM runtime.

### Blazor WASM vs Server — تنبيه Playwright

| Blazor Server | Blazor WASM (Ejar) |
|---|---|
| HTML من الخادم عبر SignalR — Playwright يرى DOM فوراً | WASM يُنزَّل ثمّ يستبدل DOM؛ Playwright يحتاج `waitForSelector` |
| `verify-runtime.mjs` يعمل بلا تعديل | `WAIT_SELECTOR` env var مطلوب (راجع قسم Ejar) |

### القيود في Claude Web sandbox

- `dotnet run` و أيّ شيء يربط port يُقتَل بـ exit code 144 (PITFALLS T2).
  → الاختبارات الـ runtime + الطبقة 6 تتطلّب جلسة محلّيّة (terminal خارج
  Claude) أو CI خارجيّ.
- `dotnet build` و `dotnet test` و الطبقات ١-٥ تعمل تماماً في الـ sandbox.

---

## عشير القديم — تحميل المشروع المرجعي

**"عشير القديم"** هو تطبيق عشير الأصلي (قبل منصة ACommerce) الموجود في
مستودع منفصل على GitHub. يُستخدَم مرجعاً للمقارنة مع Ashare V2 في هذا
المستودع — للتحقّق من مطابقة الواجهة والسلوك.

> **تحذير:** "عشير القديم" لا يعني `Apps/Ashare.Api` أو `Apps/Ashare.Web`
> الموجودَين في هذا المستودع. تلك نسخ مبكّرة **على نفس منصة ACommerce**،
> وليست المشروع الأصلي. "عشير القديم" = المستودع الخارجي أدناه فقط.

### الخطوات

```bash
# 1. تأكّد أن .NET مثبَّت (انظر القسم الأعلى)
dotnet --version    # يجب أن يطبع 10.0.x

# 2. استنسخ المشروع الأصلي إلى /tmp
git clone https://github.com/acommerce-lab/ACommerce.Libraries /tmp/ACommerce.Libraries

# 3. تحقّق من الهيكل
ls /tmp/ACommerce.Libraries
```

عادةً ما يكون المشروع في `/tmp/ACommerce.Libraries` طوال الجلسة.
عند الإشارة إلى "عشير القديم" في أي مهمة، ابحث أولاً في هذا المسار.
