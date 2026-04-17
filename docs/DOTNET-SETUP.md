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
