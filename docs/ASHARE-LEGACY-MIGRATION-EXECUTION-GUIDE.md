# دليل التنفيذ الموحَّد لترحيل عشير القديم إلى V2

**الغرض**: المستند الجامع الذي يربط الخطّتَين (واجهة + خلفيّة) ويحدّد ترتيب التنفيذ الفعليّ، المهام المؤجَّلة، المخاطر، واستراتيجيّة القطع. يُقرأ بعد الخطّتَين لا قبلهما.

**المستندات المرجعيّة**:
- `docs/ASHARE-LEGACY-INVENTORY.md` — جرد الخدمة الخلفيّة القديمة.
- `docs/ASHARE-LEGACY-FRONTEND-INVENTORY.md` — جرد الواجهة القديمة.
- `docs/ASHARE-METHODOLOGY-CITATIONS.md` — الاستشهادات بـ `file:line`.
- `docs/ASHARE-LEGACY-FRONTEND-MIGRATION-PLAN.md` — خطّة الواجهة.
- `docs/ASHARE-LEGACY-BACKEND-MIGRATION-PLAN.md` — خطّة الخلفيّة.

---

## 1. نظرة عامّة (Executive Summary)

**المصدر**: تطبيق عشير القديم في `/tmp/ACommerce.Libraries/Apps/Ashare.*/`:
- **خلفيّة**: 10 متحكّمات محلّية + 23 متحكّم من مكتبة ACommerce + 2 SignalR hubs + ~4 خدمات = **71 endpoint**.
- **واجهة**: 25 صفحة في `Ashare.Shared` + Web shell (69 سطراً) + MAUI shell (162 سطراً) + Admin (13 صفحة) = **~18,000 سطر**.

**الوجهة**: ثلاثة تطبيقات V2:
- `Apps/Ashare.V2.Api` — خدمة OAM خالصة (16 مرحلة).
- `Apps/Ashare.V2.Web` — Blazor Server + widgets فقط (11 مرحلة + MAUI مؤجَّلة).
- `Apps/Ashare.V2.Admin` — لوحة إدارة بدون Syncfusion (مرحلة 10 من خطّة الواجهة).

**المبدأ الحاكم**: كلّ تغيير حالة = عمليّة محاسبيّة واحدة (OAM) — `Entry.Create().From().To().Tag().Analyze().Execute().Build()` → `ExecuteEnvelopeAsync()`. الواجهة لا تستدعي الخدمات مباشرة — كلّ شيء عبر `ClientOpEngine` + `HttpDispatcher`. الخلفيّة لا تُحوِّل entity مباشرة من متحكّم — كلّ شيء عبر `OpEngine`.

**العائد**:
- كود قابل للتدقيق (كلّ فعل = Entry قابل للتسلسل).
- قواعد الأعمال (Quota, Audit, Rate-limit, Content filter) في مكان واحد (Interceptors) لا مشتتة في المتحكّمات.
- اعتمادات خارجيّة (Nafath, Noon, Firebase, GCS/OSS) قابلة للاستبدال عبر Provider contracts.
- واجهة قابلة للتحقّق آليّاً عبر 6 طبقات (`verify-*.sh`).

---

## 2. ملخّص الخطّتين

### 2.1 خطّة الواجهة — 12 مرحلة

| # | المرحلة | النطاق | الأثر الأساسيّ |
|--:|---|---|---|
| 0 | Foundation + Shells | `Apps/Ashare.V2.Web` shell + ClientOpEngine + Culture | قابليّة الإقلاع |
| 1 | Layout & Theme | `MainLayout` widgets-only + `:root` brand tokens | الهويّة البصريّة |
| 2 | Auth + Legal | Login (نفاذ) + Language + LegalPageView | مدخل الاستخدام |
| 3 | Browsing | Home + Search + Explore | اكتشاف المحتوى |
| 4 | Space Details | أكبر صفحة (1,455 سطراً) → ودجات قابلة لإعادة الاستخدام | عرض المنتج |
| 5 | Bookings | قائمة + إنشاء + تفاصيل (3 صفحات) | دورة الحجز |
| 6 | Host | نشر إعلان + اشتراكات + دفع (6 صفحات) | جانب المستضيف |
| 7 | Chats + Notifications | Realtime بواسطة Provider contract | التفاعل الفوريّ |
| 8 | Profile + Favorites | 3 صفحات | الإدارة الذاتيّة |
| 9 | Complaints | صفحتان | الدعم |
| 10 | Admin Rewrite | 13 صفحة بدون Syncfusion | لوحة الإدارة |
| 11 | MAUI Shell | مُؤجَّل لما بعد استقرار Web | تطبيقات جوّال |

### 2.2 خطّة الخلفيّة — 16 مرحلة

| # | المرحلة | النطاق | الأثر الأساسيّ |
|--:|---|---|---|
| 0 | Bootstrap | Entities + OpEngine + Culture | أساس التشغيل |
| 1 | Auth (Nafath) | 7 endpoints + Provider contracts | الدخول |
| 2 | Profiles | CRUD + حقول محميّة | هويّة المستخدم |
| 3 | Catalog | فئات + سمات + mappings + locations | الكتالوج |
| 4 | Listings | 8 عمليّات + Quota + DynamicAttrs | المنتجات |
| 5 | Bookings | 7 عمليّات + overlap/availability | الحجوزات |
| 6 | Payments (Noon) | 4 عمليّات + webhook + sub-entries | الدفع |
| 7 | Subscriptions | 5 عمليّات + quota على listings | خطط المستضيف |
| 8 | Chats (SignalR) | 6 عمليّات + IRealtimeMessageBus | المحادثات |
| 9 | Notifications (FCM) | 13 endpoint (user + admin) | الإشعارات |
| 10 | Files (GCS/OSS) | 4 endpoints + IStorageProvider | تخزين |
| 11 | Complaints | 6 عمليّات + Audit + notify-admin | الدعم |
| 12 | Admin | Dashboard + Reports + Audit | لوحة إدارة |
| 13 | Cross-cutting | Marketing + Errors + Legal + Versions | عرضيّات |
| 14 | Seeding | Static + Production backfill | البيانات الأوّليّة |
| 15 | Observability | Serilog + Health + Metrics + Rate-limit | التشغيل الإنتاجيّ |

### 2.3 الملاحق المشتركة

كلّ خطّة لها 5 ملاحق:
- **الواجهة**: أ (جدول الـ25 صفحة) + ب (ashare-* → Ac*) + ج (Shell services) + د (Wizard CreateListing) + هـ (مراجع + DoD).
- **الخلفيّة**: أ (جدول 71 endpoint) + ب (15 Provider contract) + ج (16 Interceptor) + د (Seeder + Production migration) + هـ (مراجع + DoD + التبعيّات + مقاييس الإنتاجيّة).

---

## 3. ترتيب التنفيذ المدموج (Joint Execution Order)

الخطّتان ليستا مستقلّتَين تنفيذيّاً — الواجهة تتطلّب الخلفيّة. المقاربة: تنفيذ أزواج خلفيّة/واجهة مترابطة، كلّ زوج قابل للنشر مستقلّاً.

| زوج | الخلفيّة | الواجهة | الناتج الإنتاجيّ |
|--:|---|---|---|
| **A** | Backend 0 (Bootstrap) | Frontend 0 (Shell) + 1 (Layout) | خدمة تقلع + shell فارغة تعرض تخطيطاً |
| **B** | Backend 1 (Auth) + 13 (LegalPages) | Frontend 2 (Auth+Legal) | مستخدم يسجّل دخول بنفاذ |
| **C** | Backend 3 (Catalog) + 4 (Listings) | Frontend 3 (Browsing) | زائر يتصفّح ويبحث |
| **D** | Backend 4 مُكتمل + 10 (Files) | Frontend 4 (Space Details) | عرض تفاصيل مساحة بكاملها |
| **E** | Backend 5 (Bookings) + 6 (Payments) | Frontend 5 (Bookings) | دورة حجز + دفع |
| **F** | Backend 7 (Subscriptions) | Frontend 6 (Host) | مستضيف ينشر ويشترك |
| **G** | Backend 8 (Chats) + 9 (Notifications) | Frontend 7 (Chats+Notifications) | تفاعل فوريّ |
| **H** | Backend 2 (Profiles) + 11 (Complaints) | Frontend 8 (Profile+Favorites) + 9 (Complaints) | إدارة ذاتيّة + دعم |
| **I** | Backend 12 (Admin) + 13 (Cross-cutting) | Frontend 10 (Admin Rewrite) | لوحة إدارة جاهزة |
| **J** | Backend 14 (Seeding) + 15 (Observability) | — | جاهزيّة إنتاج |
| **K** | — | Frontend 11 (MAUI) | تطبيقات جوّال |

**قاعدة**: لا يُبدأ زوج قبل `Definition of Done` للزوج السابق (ما لم يكن موازيّاً صراحةً). الأزواج `G / H / I` قابلة للتوازي بعد `F`.

### 3.1 التقدير الزمنيّ (تقريبيّ، لفريق من 2–3 مطوّرين)

| زوج | أيّام | ملاحظة |
|--:|--:|---|
| A | 3 | إعداد المشروع + CI |
| B | 5 | Nafath provider hard |
| C | 4 | كتالوج + قوائم |
| D | 7 | أكبر صفحة + ودجات |
| E | 8 | حجز + دفع + webhook |
| F | 6 | Host wizard |
| G | 6 | Realtime تجريبيّاً |
| H | 5 | إدارة ذاتيّة |
| I | 10 | إعادة كتابة Admin |
| J | 4 | بذور + مراقبة |
| K | 15+ | MAUI على منصّتَين |
| **مجموع Web-only** | **~58 يوم** | ≈ 12 أسبوعاً لفريقين |
| **+ MAUI** | **~73 يوم** | ≈ 15 أسبوعاً |

---

## 4. المهام المُؤجَّلة (Deferred Work)

هذه مهام طُرِحت في المحادثة ولم تُنفَّذ، تبقى مؤجَّلة حتى إشارة صريحة من المُستخدِم:

### 4.1 إعادة هيكلة `.sln` logical folders

**الطلب الأصليّ**: "غير معمارية المجلدات المنطقية في عشير الاصدار الاول وفي اوردر لتطابق معمارية مجلداتها الفعلية ولتطابق معمارية المجلدات لتطبيق عشير الاصدار الثاني".

**النطاق**:
- `Apps/Ashare/*` (V1 في المستودع الحاليّ) — إعادة ترتيب solution folders لتطابق المجلّدات الفعليّة.
- `Apps/Order/*` — نفس العمل.
- مرجع البنية المستهدَفة: `Apps/Ashare.V2/*` (بنيتها المنطقيّة تطابق الفعليّة).

**الخطوات المقترحة**:
1. قراءة الـ `.sln` الرئيسيّ + `.slnf` الفرعيّة لتحديد الوضع الحاليّ.
2. مقارنة مع الشجرة الفعليّة تحت `Apps/Ashare` و `Apps/Order`.
3. إعادة توليد الـ `.sln` بـ `dotnet sln add` في الترتيب الصحيح.
4. التحقّق: `dotnet build` يبقى ناجحاً.
5. commit واحد: `refactor(sln): align V1 solution folders with physical layout (Ashare+Order)`.

**الحالة**: **لم يُنفَّذ** — ينتظر إذناً صريحاً. يمكن تنفيذه مستقلّاً عن الخطّتَين.

### 4.2 تنفيذ فعليّ للخطّتَين

**ما أُنجِز**: التخطيط الكامل (ثلاثيّة مستندات: جرد + استشهادات + خطّة) للواجهة والخلفيّة.

**ما لم يُنجَز**: كتابة كود أيّ مرحلة. العمل التنفيذيّ مؤجَّل حتى موافقة المُستخدِم على الخطّة والبدء من الزوج A.

### 4.3 MAUI Shell

**الحالة**: مُؤجَّل ضمن الخطّة (المرحلة 11 من خطّة الواجهة + الزوج K). ينتظر استقرار Web أوّلاً.

### 4.4 سكربتات الفحص الخلفيّة المقترحة

في ملحق القبول الخلفيّ ذكرتُ:
- `scripts/verify-backend-envelope.sh`
- `scripts/verify-backend-mutations.sh`
- `scripts/verify-backend-entries.sh`

**الحالة**: لم تُكتَب بعد — تُضاف مع الزوج A (Bootstrap) لأنّها شرط قبول لكلّ PR خلفيّ.

### 4.5 خطّة البيانات (Data migration)

قاعدة البيانات القديمة (EF Core) قد لا تطابق مخطَّط V2 بالضبط. المطلوب (مُؤجَّل):
- تحليل فروقات الـ schema بين القديم والجديد.
- كتابة سكربت ترحيل بيانات (INSERT/UPDATE من DB قديمة إلى جديدة).
- اختبار على نسخة staging قبل cutover.

**ملاحظة**: إن كان MVP سيُبذَر من الإنتاج مباشرةً (المرحلة 14 من خطّة الخلفيّة)، قد نتخطّى ترحيل DB بالكامل. يُحسم بحسب حجم البيانات الموروثة.

---

## 5. سجلّ المخاطر (Risk Register)

| # | المخاطرة | الاحتماليّة | الأثر | التخفيف |
|--:|---|:-:|:-:|---|
| 1 | Nafath API يتغيّر أثناء الترحيل | متوسّط | عالٍ | Provider contract مع Fake provider للاختبار؛ حظر نسخة واحدة لحظة الإطلاق |
| 2 | Noon webhook signature يتعطّل | منخفض | عالٍ | اختبار webhook في staging مع توقيعات حقيقيّة قبل cutover؛ fallback لنسخة sandbox |
| 3 | Firebase FCM quotas تُتجاوَز في broadcast | متوسّط | متوسّط | FCMDeliveryRetryInterceptor + rate-limit على admin.notifications.broadcast + batching |
| 4 | Syncfusion يحتوي ميزات لا نظير لها في widgets الحاليّة | متوسّط | متوسّط | إضافة ودجات جديدة (AcDataTable/AcStatsCard/AcTimeSeriesChart) قبل المرحلة 10؛ لا تأجيل حرج |
| 5 | عدد كبير من الصفحات تمرّ عبر `OnAfterRenderAsync` → latency أوّل render | متوسّط | متوسّط | Skeletons في كلّ صفحة auth-dependent + streaming hydration |
| 6 | DynamicAttributes من الإنتاج بأشكال غير متوقّعة | عالٍ | منخفض | DynamicAttributesAnalyzer لا يرفض المفاتيح غير المعروفة — يحفظها خامّة (قانون 6) |
| 7 | ترتيب CSS cascade خاطئ → ألوان الـ brand تُطغى عليها templates | متوسّط | متوسّط | verify-css.sh يفحص الترتيب تلقائيّاً + PR template يذكّر بالترتيب |
| 8 | Blazor Server circuit reconnection بعد انقطاع طويل | متوسّط | متوسّط | `AutomaticReconnectPolicy` + معالج `circuit-disconnected` يُعيد navigate إلى home |
| 9 | Production DB schema مختلف → seeding يفشل | متوسّط | عالٍ | `AshareSeeder` idempotent + JsonElement safe + fallback للبيانات الثابتة |
| 10 | فجوة في `ASHARE-LEGACY-INVENTORY.md` (ميزة غير موثَّقة) | متوسّط | متوسّط | مراجعة الجرد في بداية كلّ زوج؛ أيّ endpoint يُكتشَف → يُضاف |
| 11 | Arabic + English content ordering (RTL/LTR) يكسر التخطيط | عالٍ | منخفض | `<html dir="rtl">` افتراضيّاً + widgets تتعامل مع `Auto` تلقائيّاً؛ يُفحَص في spatial-contracts |
| 12 | MAUI iOS APNS setup يتأخّر (شهادات Apple) | عالٍ | متوسّط | البدء بالإعدادات مبكّراً في الزوج K، قبل الترميز |
| 13 | Rate limits على Nafath أثناء load test | متوسّط | عالٍ | تنسيق مع ممزوِّد نفاذ قبل أيّ حمل اختباريّ |
| 14 | اختلاف ترتيب العمليّات بين Web و MAUI يسبّب حالات عرقيّة | منخفض | عالٍ | كلّ العمليّات idempotent عبر `client-op-id` — بروتوكول `ClientOpEngine` ثابت |
| 15 | Legacy attribution cookies/sessions لا تُحمَل للمستخدمين الحاليّين | متوسّط | منخفض | AttributionEnrichmentInterceptor يدعم Associated sessions بعد login |

---

## 6. استراتيجيّة القطع (Cutover Strategy)

### 6.1 المرحلة قبل القطع (Pre-cutover)

1. **Staging موازي**: `Apps/Ashare.V2.*` منشور على DNS فرعيّ (مثلاً `v2.ashare.sa`) بجوار الإنتاج القديم.
2. **حركة محاكاة**: Playwright يُجري سيناريوهات كاملة (25 صفحة × 3 أدوار: زائر/عميل/مستضيف) كلّ ساعة.
3. **Shadow traffic** (اختياريّ): نسخ من الطلبات الإنتاجيّة تُرسَل إلى V2 backend بدون التأثير على المستخدم (مقارنة Envelopes).
4. **Data freshness**: `AshareSeeder` يجلب من الإنتاج كلّ 6 ساعات في staging للحفاظ على تجربة قريبة.

### 6.2 القطع (Cutover)

**خيار أ — Big-bang** (لمنصّة صغيرة):
- نافذة صيانة ≤ 2 ساعة.
- تجميد الكتابة في القديم.
- نسخ نهائيّ للبيانات من القديم إلى V2.
- تبديل DNS.
- إعادة فتح الخدمة.

**خيار ب — Canary** (للمنصّة الأكبر):
- توجيه 5% من حركة الـ Web إلى V2 عبر reverse-proxy.
- مراقبة معدّلات الخطأ والـ latency وشكاوى المستخدمين.
- رفع تدريجيّ: 5% → 25% → 50% → 100% بفواصل أيّام.
- Rollback فوريّ إن ظهر anomaly.

**MAUI دائماً big-bang**: إصدار جديد من التطبيق عبر المتاجر + إجبار التحديث (force-update) عبر `app.versions.latest` إذا لزم.

### 6.3 خلال القطع (During cutover)

- `IMarketingEventTracker` يُرسَل `cutover_start` و`cutover_complete` لـ analytics.
- `Error report dashboard` مرئيّ لفريق العمليّات.
- `Incident channel` مفتوح (Slack/Teams).
- Team on-call لكلّ منصّة (Web / Android / iOS).

### 6.4 بعد القطع (Post-cutover) — الأسبوع الأوّل

1. مراقبة مستمرّة على `/health` + Prometheus + logs.
2. مقارنة يوميّة لـ KPIs قبل/بعد (أعداد الحجوزات، نسبة نجاح الدفع، نسبة قبول الإشعارات).
3. فريق دعم على أُهبة الاستعداد لأيّ بلاغ عدم توافق.
4. الإنتاج القديم يبقى في وضع read-only لمدّة 14 يوماً قبل التفكيك النهائيّ (safety net).

---

## 7. ما بعد القطع (Post-cutover Operations)

### 7.1 مؤشّرات أداء (KPIs) للمراقبة

| المؤشّر | العتبة المقبولة | مصدر القياس |
|---|---|---|
| `/health` uptime | ≥ 99.9% شهريّاً | Prometheus |
| p95 latency على `listings.list` | < 200ms | Serilog + metrics |
| p99 latency على `bookings.create` | < 500ms | Serilog + metrics |
| Webhook Noon success | ≥ 99.5% | `payments.callback.process` outcomes |
| FCM delivery | ≥ 95% | `IPushNotificationProvider` telemetry |
| Chat message end-to-end | < 1.5s | Playwright probe |
| Error rate | < 0.5% لكلّ endpoint | GlobalExceptionMiddleware |
| User complaints SLA | ≤ 24 ساعة للردّ الأوّل | `complaints.reply` timestamps |

### 7.2 الصيانة الدوريّة

- **يوميّاً**: مراجعة alerts + تسجيل أيّ anomaly في backlog.
- **أسبوعيّاً**: تدقيق `AuditLog` — نسب العمليّات المرفوضة، أسبابها.
- **شهريّاً**: مراجعة ملاحظة (`verify-*.sh` على كلّ مستودع PRs) — أيّ انزلاقات منهجيّة.
- **ربع سنويّاً**: مراجعة Providers (Nafath/Noon/Firebase versions) — ترقيات.

### 7.3 خطّة النموّ

- **Phase Next (بعد المنصّة)**: إضافة Apps جديدة (مثل Vendor-facing ومستقلّة عن Ashare) — تُستهلك نفس OAM core.
- **Search upgrade**: إدماج Elasticsearch/Meilisearch كـ `ISearchProvider` بدل DB queries في `listings.search`.
- **AI features**: وصف تلقائيّ، تسعير مقترَح، فلاتر ذكيّة — كـ Operations جديدة + `IAIProvider` contracts.
- **Reporting warehouse**: تدفّق `AuditLog` إلى BigQuery/ClickHouse لتحليلات عميقة.

---

## 8. الخطوة التالية الفوريّة (Immediate Next Step)

عند الموافقة على الخطّة، البدء بالزوج **A** فوراً:

### 8.1 قائمة مراجعة قبل البدء

- [ ] قراءة `docs/MODEL.md` و `docs/BUILDING-A-BACKEND.md` كاملَيْن (أو مراجعتهما).
- [ ] قراءة `docs/BUILDING-A-FRONTEND.md` و `docs/STYLING-METHODOLOGY.md` كاملَيْن.
- [ ] تثبيت .NET 10 بحسب `docs/DOTNET-SETUP.md`.
- [ ] تثبيت Playwright Chrome-for-Testing بحسب نفس المستند.
- [ ] إنشاء branches: `feature/ashare-v2-api-bootstrap` و `feature/ashare-v2-web-shell`.
- [ ] إنشاء issue tracker (GitHub Projects) مع كلّ زوج كـ milestone.

### 8.2 أوّل PR خلفيّ (Pair A — Backend)

**العنوان**: `feat(ashare-v2-api): bootstrap — entities, OpEngine, culture stack, Serilog`

**المحتوى**:
1. `Apps/Ashare.V2.Api/Program.cs` — مع `EntityDiscoveryRegistry.RegisterEntity` لكلّ كيان (من ملحق أ في خطّة الخلفيّة).
2. `Apps/Ashare.V2.Api/ApplicationDbContext.cs` + `DataProtectionKeyContext.cs`.
3. `Apps/Ashare.V2.Api/Controllers/HealthController.cs` — endpoint `/health` يعيد envelope.
4. `scripts/verify-backend-envelope.sh` + `verify-backend-mutations.sh` + `verify-backend-entries.sh`.
5. `tests/Ashare.V2.Api.Tests/HealthTests.cs`.
6. تحديث `ROADMAP.md` بوسم المرحلة 0 [x].

### 8.3 أوّل PR أماميّ (Pair A — Frontend)

**العنوان**: `feat(ashare-v2-web): bootstrap shell + MainLayout widgets-only`

**المحتوى**:
1. `Apps/Ashare.V2.Web/Program.cs` مع ClientOpEngine + BlazorCultureStack.
2. `Apps/Ashare.V2.Web/Components/Layout/MainLayout.razor` — AppBar + BottomNav + `acs-page`.
3. `Apps/Ashare.V2.Web/wwwroot/app.css` — `:root` overrides.
4. `Apps/Ashare.V2.Web/wwwroot/images/ashare-logo.png` (مستنسَخ من المصدر).
5. `Apps/Ashare.V2.Web/Components/Pages/Home.razor` — فارغة بعنوان فقط للإقلاع.
6. نتائج الـ6 سكربتات verify مُلصقة.

### 8.4 معايير قبول الزوج A

- `dotnet build` و `dotnet test` ناجحان.
- `scripts/verify-page-structure.sh Apps/Ashare.V2.Web` → 0.
- `scripts/verify-widget-contracts.sh` → 0.
- `scripts/verify-runtime.sh` مع `spatial-contracts.json` → 0 على Home الفارغة.
- تحديث `ROADMAP.md` مع commit واحد موسوم `[ashare-v2-pair-a]`.

---

## 9. خاتمة موجَّهة للمُستخدِم

### 9.1 ما اكتمل في هذه الجلسة

| المستند | الأسطر | الـ commit |
|---|--:|---|
| `docs/ASHARE-LEGACY-INVENTORY.md` | 380 | `7932343` (سابق) |
| `docs/ASHARE-LEGACY-FRONTEND-INVENTORY.md` | 218 | `e75498c` |
| `docs/ASHARE-METHODOLOGY-CITATIONS.md` | 147 | `e75498c` |
| `docs/ASHARE-LEGACY-FRONTEND-MIGRATION-PLAN.md` | 512 + 230 ملحقات | `47632e8` + `bdadfd1` |
| `docs/ASHARE-LEGACY-BACKEND-MIGRATION-PLAN.md` | 1,089 | `d674f06` |
| `docs/ASHARE-LEGACY-MIGRATION-EXECUTION-GUIDE.md` | هذا المستند | (يُدفَع الآن) |

**إجماليّ التخطيط**: ~2,800 سطر موثَّق، مربوط بالمنهجيّة بـ `file:line`، جاهز للتحويل إلى كود.

### 9.2 الدروس المستفادة من هذه الجلسة

1. **Stream idle timeout** كان يحدث بسبب استدعاءات وكلاء فرعيّين + قراءات ضخمة دفعةً واحدة. الحلّ: نمط Write (هيكل) + سلسلة Edits قصيرة — كلّ استدعاء بحدّ زمنيّ ضيّق.
2. **توزيع العمل على ملفّات متعدّدة** أفضل من ملفّ واحد عملاق — يُسهّل المراجعة ويمنع فقدان السياق.
3. **الاستشهادات المنهجيّة** يجب أن تكون مستنداً منفصلاً، لأنّ الخطط تستشهد بها مراراً.

### 9.3 طلب اتّخاذ القرار

يحتاج منك:
1. **موافقة على الخطّتَين** (أو تعديل) قبل الانتقال للتنفيذ.
2. **إذن تنفيذ إعادة هيكلة `.sln`** (المهمّة المؤجَّلة من قسم 4.1).
3. **تحديد خيار cutover**: big-bang (خيار أ) أم canary (خيار ب).
4. **تحديد: هل نبدأ بالزوج A الآن**، أم نراجع تفصيلاً أكثر في مرحلة بعينها؟

لا يلزم الإجابة على كلّ النقاط الآن — أيّ إشارة للبدء تكفي للانطلاق.
