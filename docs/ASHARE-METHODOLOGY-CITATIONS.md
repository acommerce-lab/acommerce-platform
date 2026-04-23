# استشهادات المنهجيّة — لخطط ترحيل عشير

ملفّ عمل يضمّ القواعد الحاكمة في المستودع الحاليّ، مُقتبسةً حرفيّاً مع `file:line`، يُستَشهَد به في خطط الترحيل (واجهة + خلفيّة).

---

## CLAUDE.md — القوانين الخمسة

- **القانون 1** — كلّ تغيير في الحالة = عمليّة (CLAUDE.md:41-53):
  > "Never write `_repo.AddAsync(entity)` from a controller. Every mutation: `var op = Entry.Create(\"thing.create\").From(...).To(...).Tag(...).Analyze(...).Execute(async ctx => await _repo.AddAsync(entity, ctx.CancellationToken)).Build(); var envelope = await _engine.ExecuteEnvelopeAsync(op, entity, ct);`"

- **القانون 2** — كلّ استجابة = `OperationEnvelope` (CLAUDE.md:56-58):
  > "Read endpoints too: `return this.OkEnvelope(\"thing.list\", data);`"

- **القانون 3** — توقيعات الـ Repository (CLAUDE.md:60-64):
  > "`ListAllAsync(ct)` — takes CancellationToken. `GetAllWithPredicateAsync(predicate)` — does NOT take CancellationToken. Second arg is `bool includeDeleted`. Mixing them is the #1 bug."

- **القانون 4** — نظام الأنماط بالـ widgets cascade (CLAUDE.md:68-69):
  > "Use `var(--ac-primary)` or Bootstrap classes. Never hard-coded colours. Brand overrides go in the app's `wwwroot/app.css` on `:root`."

- **القانون 5** — حالة المصادقة تنجو بعد reload عبر `OnAfterRenderAsync` (CLAUDE.md:71-74):
  > "Load auth-dependent data in `OnAfterRenderAsync(firstRender: true)` after `await Auth.EnsureRestoredAsync()`. Not in `OnInitializedAsync`."

- **القانون 6** — التكيّف مع بيانات الإنتاج (CLAUDE.md:76-85):
  > "The NEW platform adapts to the shape of production data — not the other way around… Any attribute key not in the template is preserved as a raw `DynamicAttribute` entry."

---

## MODEL.md — محرّك OAM

- بنية الـ Entry الإلزاميّة (MODEL.md:65-82):
  > "Entry = { Type, Parties, Tags, Analyzers, Execute, SubEntries, Relations }. `AccountingBuilder` يفرض: على الأقلّ `.From()` + `.To()`، `BalanceAnalyzer` تلقائيّ، وسم `pattern: accounting`."

- التمييز Analyzer vs Interceptor (MODEL.md:83-123):
  > "Analyzer: local constraint bound to a single entry via `.Analyze()` or `.PostAnalyze()`. Interceptor: global, registered in DI, applied to any matching entry at runtime."

- حصر الحقن (MODEL.md:122-123):
  > "`entry.Sealed()` blocks all interceptors. `entry.ExcludeInterceptor(\"name\")` blocks one by name."

- تعريف ProviderContract (MODEL.md:125-142):
  > "Mandatory external dependency… Mandatory/Explicit/Typed (`.Requires<T>()`)."

---

## LIBRARY-ANATOMY.md — النمط ثلاثيّ الطبقات

- **الطبقة 1 — محاسبة نقيّة** (LIBRARY-ANATOMY.md:33-44): `Entry type catalog / Local analyzers / Relations / Tags`.
- **الطبقة 2 — ProviderContracts** (LIBRARY-ANATOMY.md:69-127): واجهات إلزاميّة، المكتبة تشحن بصفر تنفيذ.
- **الطبقة 3 — Interceptors المحقونة** (LIBRARY-ANATOMY.md:128-155): يسجّلها التطبيق المستهلك ويطبقها المحرّك تلقائيّاً عبر مطابقة الوسوم.
- المعترضات الشائعة (LIBRARY-ANATOMY.md:148-155): Quota, Audit, Translation, Rate-limit, Content filtering, Journaling.

---

## BUILDING-A-BACKEND.md — وصفة خدمة خلفيّة

- تسجيل الكيانات (BUILDING-A-BACKEND.md:112-115):
  > "`EntityDiscoveryRegistry.RegisterEntity(typeof(User)); … typeof(TwoFactorChallengeRecord);`"
- تسجيل OpEngine (BUILDING-A-BACKEND.md:135-136):
  > "`builder.Services.AddScoped<OpEngine>(sp => new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));`"
- قواعد الـ Repository (BUILDING-A-BACKEND.md:286-289):
  > "`GetAllWithPredicateAsync(predicate, bool includeDeleted)` وليس `CancellationToken`. `ListAllAsync(ct)` يأخذ `CancellationToken`."
- نمط Controller للطفرات (BUILDING-A-BACKEND.md:295-350):
  > "`Entry.Create(\"thing.create\").Describe(...).From(...).To(...).Tag(...).Analyze(...).Execute(async ctx => { await _repo.AddAsync(...); ctx.Set(...); }).Build();` → `_engine.ExecuteEnvelopeAsync(op, entity, ct);`"
- نمط Seeder (BUILDING-A-BACKEND.md:388-410): idempotent (check-by-ID)، يتكيّف مع شكل بيانات الإنتاج.

---

## BUILDING-A-FRONTEND.md — وصفة واجهة Blazor

- استعادة JWT بعد reload (BUILDING-A-FRONTEND.md:271-274):
  > "`protected override async Task OnAfterRenderAsync(bool firstRender) { if (firstRender) await Auth.EnsureRestoredAsync(); }`"
- ترتيب CSS cascade (BUILDING-A-FRONTEND.md:160-167): `widgets.css → templates.css → app.css (brand) → bootstrap-icons.min.css`.
- ملفّ العلامة (BUILDING-A-FRONTEND.md:176-208): تعديل `:root` فقط لـ `--ac-primary/--ac-bg/...` + `html[data-theme="dark"]` overrides.
- صفحة تعتمد على الـ Auth (BUILDING-A-FRONTEND.md:317-374):
  > "Any page conditional on `Auth.IsAuthenticated` loads data in `OnAfterRenderAsync(firstRender: true)` and shows a skeleton until then."

---

## STYLING-METHODOLOGY.md — قوانين التصميم

- **قانون 1** (STYLING-METHODOLOGY.md:28-44): صفحات تستخدم templates وwidgets فقط.
  > "❌ `<button class=\"btn btn-primary\">`, `<div class=\"card\">`. ✅ `<AcButton>`, `<AcCard>`."
- **قانون 2** (STYLING-METHODOLOGY.md:50-56): كلّ widget يُعلن فئاته في header: `@* CSS_CLASSES: … *@`.
- **قانون 3** (STYLING-METHODOLOGY.md:62-70): كلّ فئة مُستخدَمة يجب أن تُعرَّف في CSS — يُفرضه `scripts/verify-css.csx`.
- **قانون 4** (STYLING-METHODOLOGY.md:72-81): `app.css` يعدّل `:root` فقط؛ لا يعيد تعريف widgets.
- **قانون 5** (STYLING-METHODOLOGY.md:82-98): عرض الصفحة عبر `<div class="acs-page">` أو `acs-page-wide`.

---

## DESIGN-CRITERIA.md — معايير القبول

- بنية (DESIGN-CRITERIA.md:11): Grep للـ HTML الخام + فئات Bootstrap → `verify-page-structure.sh`.
- وجود الـ classes (DESIGN-CRITERIA.md:12): استخراج `class="..."` ومقارنتها بالـ CSS → `verify-css.sh`.
- سقف اللوحة اللونيّة (DESIGN-CRITERIA.md:13): عدد قيم hex مميّزة ≤ 60.
- منع الألوان الخامّة (DESIGN-CRITERIA.md:14): لا `style="color:#..."` خارج `var(--ac-*)`.
- سلّم الخطوط (DESIGN-CRITERIA.md:15): `10/11/12/13/14/15/16/18/20/24/28/32/40/48 px` فقط.
- سلّم التباعدات (DESIGN-CRITERIA.md:16): مضاعفات 4 (0,4,8,12,16,20,24,32,40,48).

---

## VERIFICATION-LAYERS.md — طبقات التحقّق السّت

- **Layer 1 — Code Hygiene** (VERIFICATION-LAYERS.md:86): يمنع `<button>` الخام + فئات Bootstrap + ألوان hex inline. **Blocks CI**.
- **Layer 2 — Class Existence** (VERIFICATION-LAYERS.md:91): كلّ class في razor له قاعدة CSS. **Blocks CI**.
- **Layer 3 — Per-Value Scale** (VERIFICATION-LAYERS.md:95-98): كلّ قيمة inline على السلّم المسموح (font-size / padding / AcIcon Size / nesting).
- **Layer 6 — Runtime Spatial Contracts** (VERIFICATION-LAYERS.md:119): Playwright يقرأ `spatial-contracts.json` ويتحقّق من المواضع/المحاذاة/التراكب.

---

## CULTURE.md — حزمة الثقافة

- تركيب الخلفيّة (CULTURE.md:42-49): `AddCultureStack()` + `UseCultureContext()` بين Routing والـ controllers.
- تركيب الواجهة (CULTURE.md:54-62): `AddBlazorCultureStack()` + `BrowserCultureProbe.InitAsync()` في `OnAfterRenderAsync`.
- `NumeralToLatinSaveInterceptor` (CULTURE.md:33-34): يحوّل الأرقام الهنديّة/الفارسيّة إلى لاتينيّة قبل الحفظ في DB.

---

## SEEDING.md — عقد البذور

- بذور مُنسَّقة (SEEDING.md:24-36): مُعرّفات ثابتة عبر الخدمات (`VendorAhmed = Guid.Parse("00000000-0000-0000-0002-000000000001")`).
- البذر من الإنتاج (SEEDING.md:137-142): `AshareSeeder` يجلب قوائم حقيقيّة من الإنتاج عند الإقلاع، ويُلحقها بالبذور المحلّية (dedupe by ID).
- أمان JsonElement (SEEDING.md:176-179): `TryGetProperty` يرمي على array elements → افحص `ValueKind != Object` أوّلاً.

---

## DYNAMIC-ATTRIBUTES.md — السمات الديناميّة

- Template + Snapshot (DYNAMIC-ATTRIBUTES.md:13-20): `Category.AttributeTemplateJson` هو الـ schema، `Listing.DynamicAttributesJson` لقطة مُجمّدة.
- مفاتيح غير معروفة (DYNAMIC-ATTRIBUTES.md:47-54): تُحفظ كـ `DynamicAttribute` خامّة بنوع مُستنتَج — لا تُحذف.

---

## ASHARE-V2-METHODOLOGY.md — نمط عشير V2

- الحكم (ASHARE-V2-METHODOLOGY.md:15-24): كلّ تغيير حالة = Operation. إمّا `Tag("client_dispatch","true")` → `HttpDispatcher`، أو مُفسّر محلّيّ.
- HTTP-bound vs Local-only (ASHARE-V2-METHODOLOGY.md:112-129): إن كانت تعيش في ذاكرة الجلسة فقط (تفضيلات/قوائم محلّية/مفضّلة) = local.
- نمط `UserCulture` الموحَّد (ASHARE-V2-METHODOLOGY.md:236-304): لغة/توقيت/عملة كوحدة واحدة ذهاباً (رؤوس) وإياباً (CultureInterceptor).

---

## ASHARE-PAGE-MIGRATION.md — قواعد ترحيل الصفحات

- القواعد الخمس غير القابلة للتفاوض (ASHARE-PAGE-MIGRATION.md:11-19):
  > "1. لا `<button>`/`<input>`/`.btn`/`.card` خام — ودجات `Ac*` فقط. 2. لا `<i class=\"bi bi-*\">` — `AcIcon` فقط. 3. لا ألوان hex في `.razor` — `var(--ac-*)`. 4. لا `AshareApiService` — كلّ متغيّر حالة = `Entry.Create(...)`. 5. كلّ استجابة خادم = `OperationEnvelope<T>`."

- Mobile-first (ASHARE-PAGE-MIGRATION.md:105-118): الصفحة تعمل داخل `max-width: 480px`؛ في 390×844 مطابقة لعشير القديم حرفيّاً.
- خطّ التحقّق السّداسيّ (ASHARE-PAGE-MIGRATION.md:121-145): كلّ النتائج `0 violations` — أيّ violation يُحلّل ويُصلَح (لا يُتجاهل).
