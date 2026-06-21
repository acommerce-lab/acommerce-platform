# نَشر مَجّاني عَلى Hugging Face Spaces + Neon

**الهَدَف:** رابِط حَيّ بِلا بِطاقَة ائتِمانيَّة، يَعمَل ٢٤/٧ مَجّاناً (يَنام
عِندَ الخُمول ثُمَّ يَصحو). المَنصَّة في Hugging Face، قاعِدَة البَيانات في Neon.

> **مُهِمّ:** هذا تَطبيق Blazor Server (مُهَيكَل) — يَحتاج عَمَلِيَّة .NET تَعمَل
> باستِمرار. GitHub Pages و Cloudflare Pages **لا يَدعَمانه** (استِضافَة ثابِتَة).

## ١. تَجهيز قاعِدَة البَيانات (Neon)

١.١. أنشِئ حِساباً على [neon.tech](https://neon.tech) (بِبَريد إلِكترونيّ — بِلا بِطاقَة).

١.٢. أنشِئ مَشروعاً جَديداً: «New Project»:
- Region: `eu-central-1` (الأَقرَب لِلسعوديَّة).
- Postgres version: 16.
- Database name: `acommerce_v1`.

١.٣. اِنسَخ سَلاسِل الاتِّصال (Connection string) — ستَجِد رابِطَين:
- **Direct connection** (لِلتَّطوير المَحَلِّيّ)
- **Pooled connection** (المُوصى بِها لِلإنتاج) — **اِستَخدِم هذِه**.

السِلسِلَة ستَكون بِصيغَة:
```
postgresql://USER:PASS@ep-xxx-pooler.eu-central-1.aws.neon.tech/acommerce_v1?sslmode=require
```

١.٤. حَوِّلها إلى صِيغَة ADO.NET (الَّتي يَفهَمُها Marten/Npgsql):
```
Host=ep-xxx-pooler.eu-central-1.aws.neon.tech;Port=5432;Database=acommerce_v1;Username=USER;Password=PASS;SSL Mode=Require;Trust Server Certificate=true
```

اِحفَظها — ستَحتاجها في الخُطوَة ٣.

## ٢. إنشاء Space عَلى Hugging Face

٢.١. اِفتَح [huggingface.co/new-space](https://huggingface.co/new-space):
- Space name: `acommerce` (أَو ما تُريد)
- License: اختَر MIT أَو Apache 2.0
- **Select Space SDK: Docker** → **Blank**
- Visibility: Public (مَجّاني)

٢.٢. اِنسَخ مَسار Space الَّذي حَصَلتَ عَليه (مَثَلاً
`https://huggingface.co/spaces/asadrahwan/acommerce`).

## ٣. ضَع الأَسرار (Secrets)

في صَفحَة الـ Space → **Settings** → **Variables and secrets** → **New secret**:

| الاسم | القيمَة |
|---|---|
| `ConnectionStrings__Postgres` | سِلسِلَة Neon مِن الخُطوَة ١.٤ (لاحِظ الـ `__` المُزدَوَج) |
| `ACOMMERCE_AUTH_SECRET` | ٣٢+ حَرفاً عَشوائيّاً — `openssl rand -base64 48` |
| `TEST_DATA_SEED` *(اختياريّ)* | `1` لِبَذر بَيانات تَجريبيَّة (مُستَخدِمين، صَفقات، مُحادَثات) — مُفيد لِلعَرض |

> **مُلاحَظَة:** ASP.NET Core يَستَبدِل `__` بِـ `:` في أَسماء الأَقسام، لِذا
> `ConnectionStrings__Postgres` يُكافِئ `ConnectionStrings:Postgres` في appsettings.

## ٤. ادفَع الكود إلى الـ Space

Space في Hugging Face هو git repo. اِربِطه بِالمَنصَّة:

```bash
# مِن مُجَلَّد المَنصَّة المَحَلِّيّ
git remote add hf https://huggingface.co/spaces/asadrahwan/acommerce

# اِدخُل برَمز HF (Settings → Access Tokens → New token → Write)
# عِندَ أَوَّل push سَيُطلَب اسم المُستَخدِم وَ token كَكَلِمَة سِرّ

# اِدفَع — قَد تَستَغرِق البِنيَة الأَولى ١٠–١٥ دَقيقَة
git push hf claude/accounting-operations-architecture-NKwE4:main
```

أَو إن أَرَدتَ مُتابَعَة فَرع `main` فَقَط:
```bash
git push hf main
```

## ٥. مُتابَعَة البِناء

اِفتَح `https://huggingface.co/spaces/asadrahwan/acommerce` — ستَرى:
1. **Building** (١٠–١٥ دَقيقَة أَوَّل مَرَّة): يَبني `Dockerfile`.
2. **Running**: التَّطبيق حَيّ.
3. اِنقُر «App» — ستَرى المَنصَّة عَلى `https://asadrahwan-acommerce.hf.space`.

أَوَّل طَلَب يَأخُذ ~٢٠ ثانِيَة (إنشاء Schemas + بَذر تَنانِير). الطَّلَبات
التاليَة سَريعَة.

## ٦. الرَّوابِط الأَوَّلِيَّة

- صَفحَة الهُبوط: `https://asadrahwan-acommerce.hf.space/`
- مَتجَر تَجريبيّ: `https://asadrahwan-acommerce.hf.space/order`
- استِكشاف ejar: `https://asadrahwan-acommerce.hf.space/ejar/explore`

إن فَعَّلتَ `TEST_DATA_SEED=1`:
- مُستَخدِمو الاختِبار:
  - عَميل order: `05201234567` / الكود `123456`
  - تاجِر order: `05211234567` / الكود `123456`
  - مُؤَجِّر ejar: `05111234567` / الكود `123456`
  - مُشرِف studio: `0501234567` / الكود `123456`

## ٧. حُدود وَ احتِياطات

| الجانِب | السَّقف المَجّاني | المَلاحَظَة |
|---|---|---|
| HF Spaces CPU | 2 vCPU / 16 GB RAM | يَنام بَعد ساعَتَين خُمول (مُعَدَّل افتراضيّ) |
| Neon Postgres | 0.5 GB، ٧٢ ساعَة compute/شَهر | كَافٍ لِعَرض تَجريبيّ + مِئات الإعلانات |
| رَفع مَلَفّات (الصُّوَر) | filesystem مُؤَقَّت | **يُحذَف عِندَ إعادَة تَشغيل الحاوِيَة** |

## ٨. إذا أَرَدتَ رَفع صُوَر يَبقى بَعد إعادَة التَّشغيل

للعَرض التَّجريبيّ، الإعلانات بِلا صُوَر مَقبولَة (المَعرَض يَعرِض placeholder
هادِئ). لاحِقاً يُمكِنُكَ:
- **Cloudflare R2** (10 GB مَجّاناً، بِلا بِطاقَة لِلتَّخزين — لكِن يَحتاج
  بِطاقَة لِتَفعيل الحِساب)، أَو
- **Backblaze B2** (10 GB مَجّاناً)، أَو
- **حَلّ مُؤَقَّت:** Hugging Face Datasets لِتَخزين صُوَر عامَّة.

ثُمَّ نَستَبدِل `AddLocalFileStorage` في `Program.cs` بِمُزَوِّد سحابيّ.

## ٩. تَحديث المَنصَّة لاحِقاً

كُلّ `git push` يُعيد البِناء تِلقائيّاً (بِفَضل HF webhooks).
