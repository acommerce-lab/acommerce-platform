# Six isolation checks + runtime smoke

أدوات للتحقّق من فرضيّة "kits معزولة عن النطاق والبيانات" — الكود +
وقت التشغيل.

## ١. الفحوصات الستّة على الكود (sandbox-friendly)

```bash
bash tools/checks/check-isolation.sh
```

كلّ فحص يُجرَى بنمط سياقيّ (يَستثني التَعليقات XML / Razor) وَيَرفض
المرور لو وجد:

| # | الفحص | يُثبت |
|---|------|-------|
| 1 | لا `MarkupString` في kit pages أو templates | ⊥ XSS |
| 2 | لا `@inject HttpClient` في kit pages | عزل البيانات |
| 3 | لا `using Ejar/Order/Ashare` في kit pages | عزل النطاق |
| 4 | كلّ `IXxxStore` له binding في `EjarCustomerHost` | اكتمال DI |
| 5 | الـ shims (Web/Maui) لا تَستورد kit/domain | حدّ أدنى للـ shim |
| 6 | kit Frontend.Customer لا يَذكُر `Ejar` | قابليّة إعادة التركيب |
| 7 | لا `@page` في `kits/*/Frontend/Customer/Widgets/` | الكيتس لا تَفرض routes — التطبيق يُجمِّع |

نَتيجة الـ branch الحاليّة: **8 passed / 0 failed**.

## ٢. تَشغيل التطبيق محلّيّاً

⚠ **لا يعمل داخل Claude Code sandbox** (Rule T2 في `CLAUDE.md`: الـ
sandbox يَقتل أيّ بَرنامج يَربط منفذاً بـ exit code 144). نَفِّذ على
جهازك المحلّيّ:

```bash
# ١. شَغِّل الـ stack الكامل (api + web)
bash tools/checks/run-locally.sh

# أو منفصل
bash tools/checks/run-locally.sh api    # http://localhost:5300
bash tools/checks/run-locally.sh web    # http://localhost:5113
```

ثمّ افتح المتصفّح على المسارات المُدرَجة في الـ script.

## ٣. لقطات شاشة عبر Playwright

```bash
npm i -D @playwright/test
npx playwright install chromium
EJAR_WEB_URL=http://localhost:5113 \
  npx playwright test --config tools/checks/playwright.config.ts
```

تُولِّد `tools/checks/screenshots/*.png`:
- 8 صفحات kit (kit-listings-explore, kit-chat-inbox, …)
- pwa-home, legacy-favorites
- مشروع `mobile-pwa` يُلقِط بأبعاد iPhone 14 لمحاكاة الـ PWA

## ٤. ربط بـ CI

أضِف للـ pipeline:

```yaml
- name: Static isolation checks
  run: bash tools/checks/check-isolation.sh
```

أيّ PR يَدخِل `MarkupString` أو `HttpClient` لـ kit page يَفشل
آليّاً — لا يَحتاج reviewer ينتبه يدويّاً.
