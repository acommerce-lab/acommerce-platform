# Platform Domain Extraction — generalized recipe

> **هذه الوثيقة لخّصت من ترحيل `Apps/Ejar/Domain`** (commit `f860873`) لتعميم
> النمط على باقي المنصّات (Ashare V2، Order V2) ولأيّ منصّة مستقبليّة.

## ما هو النمط

كلّ منصّة (Ejar, Ashare V2, Order V2) لها أكثر من خدمة خلفيّة + أكثر من
تطبيق أماميّ يشاركون **نفس الكيانات** (Listing، Conversation، Booking…).
الـ default الأوّليّ — وضعها في خدمة Customer + ProjectReference إليها من
الباقين — يخلق تبعيّة ضارّة: تعديل خدمة Customer قد يكسر Provider/Admin.

الحلّ: مكتبة `Apps/{Platform}/Domain/` صغيرة، نقيّة، يشير إليها الجميع
**مباشرةً** (لا عبر خدمة Customer).

```
Apps/{Platform}/
  ├── Domain/                              ← ⭐ هنا الكيانات والـ seed state
  │   ├── {Platform}.Domain.csproj
  │   ├── Entities/Listing.cs              ← أو records في ملف واحد
  │   ├── Entities/Conversation.cs
  │   └── Seed.cs                          ← static state (للتطوير قبل DB)
  ├── Customer/Backend/
  ├── Customer/Frontend/
  ├── Provider/Backend/
  ├── Provider/Frontend/
  ├── Admin/Backend/
  └── Admin/Frontend/
```

## ما يحقّقه

| قبل | بعد |
|---|---|
| Provider/Admin يعتمدون على `Customer.Api.csproj` كاملاً (entities + middleware + controllers) | يعتمدون على `Domain` فقط، و *اختياريّاً* على `Customer.Api` للـ middleware حتّى يُستخرَج لمكتبة منفصلة |
| تعديل ضغير في خدمة Customer قد يكسر Provider/Admin | الـ Domain stable؛ إعادة ترتيب ملفّات الخدمة لا تلامس باقي الخدمات |
| لا فصل بين "كيان نطاق" و"بنية تحتيّة لخدمة" | فصل صريح؛ EF mappings تذهب لاحقاً في `Backend/Persistence/` لا في الكيان |
| الـ Frontend يكتب DTOs مكرّرة لأنّه لا يستطيع الإشارة لـ Backend.csproj | يستطيع الإشارة لـ `Domain` ويستهلك الكيان نفسه (deserialization مجّاناً عبر System.Text.Json) |

## شروط Domain النقيّة

تذكّر دائماً:

1. **لا EF Core attributes** على الكيان (`[Table]`, `[Key]`, `[Index]`).
   تعريفات الـ schema تعيش في `IEntityTypeConfiguration<T>` داخل خدمة
   الـ Backend. الكيان POCO صرف.
2. **لا ASP.NET attributes** (`[ApiController]`, `[FromBody]`).
3. **لا تبعيّات على مزوّدات** (SignalR، Firebase، Aliyun). الـ Domain يعتمد
   فقط على:
   - مكتبات تجريد المنصّة (`ACommerce.Chat.Operations` لتنفيذ `IChatMessage`).
   - `System.*` فقط من المكتبات القياسيّة.
4. **لا منطق أعمال معقّد**. حسابات بسيطة على الكيان نفسه نعم؛ workflows لا.
   الـ workflows تذهب في خدمة الـ Backend عبر OAM operations.

## الوصفة التشغيليّة

لمنصّة موجودة (مثل Ashare V2 أو Order V2):

```bash
# 1. أنشئ Domain
mkdir -p Apps/{Platform}/Domain
cat > Apps/{Platform}/Domain/{Platform}.Domain.csproj <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>{Platform}.Domain</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\libs\backend\messaging\ACommerce.Chat.Operations\ACommerce.Chat.Operations.csproj" />
  </ItemGroup>
</Project>
EOF

# 2. انقل الكيانات (أمثلة من Ashare V2)
git mv Apps/{Platform}/Customer/Backend/{X}.Api/Services/Seed.cs \
       Apps/{Platform}/Domain/Seed.cs
git mv Apps/{Platform}/Customer/Backend/{X}.Api/Entities Apps/{Platform}/Domain/Entities

# 3. عدّل namespace
sed -i 's/namespace {X}.Api.Services;/namespace {Platform}.Domain;/' Apps/{Platform}/Domain/*.cs
sed -i 's/namespace {X}.Api.Entities;/namespace {Platform}.Domain;/' Apps/{Platform}/Domain/Entities/*.cs

# 4. سحب الاستيراد عبر كلّ الخدمات
grep -rln "{X}\.Api\.Services\|{X}\.Api\.Entities" Apps/{Platform} \
  | xargs sed -i 's/using {X}\.Api\.Services;/using {Platform}.Domain;/g; s/using {X}\.Api\.Entities;/using {Platform}.Domain;/g'

# 5. أضف ProjectReference في كلّ csproj backend
# (ابحث عن "<!-- Customer backend: shared" واضف Domain ref بجانبه)
```

## أخطاء شائعة

- **❌ نقل Middleware للـ Domain**. `CurrentUserMiddleware`, `GlobalExceptionMiddleware`
  ASP.NET-aware؛ تظلّ في خدمة Customer أو في `Apps/{Platform}/Infrastructure/`
  منفصلة لاحقاً.
- **❌ نقل Controllers للـ Domain**. الـ Controllers هي host-specific؛ تبقى
  في خدمتها أو تنقل لـ Kit (انظر `KIT-PATTERN.md`).
- **❌ جعل Domain يعتمد على EF**. حتّى DbContext يعيش في Backend مع mappings
  منفصلة. Domain نقيّ.
- **❌ مشاركة state mutable عبر العمليّات**. الـ static dictionaries في
  `Seed.cs` تعمل لـ in-process testing فقط. كلّ Backend instance له نسخته.
  للـ persistence الحقيقيّ، استبدل الـ Seed بـ EF DbContext و Repositories.

## متى تنتقل لمكتبة Infrastructure منفصلة

عندما يبدأ Provider/Admin يحتاجون الـ Middleware لكن **لا** يريدون التبعيّة على
خدمة Customer كاملةً (لأنّها تجلب controllers لا يحتاجونها)، استخرج:

```
Apps/{Platform}/
  ├── Domain/                              ← entities (موجود)
  ├── Infrastructure/                      ← ⭐ أضف
  │   ├── {Platform}.Infrastructure.csproj
  │   ├── Middleware/
  │   ├── Filters/
  │   └── Auth/                            ← shared JWT setup helpers
  └── ...
```

نقل عند الـ pain، ليس قبله (Rule of Three: استخرج بعد ٣ تكرارات).

## مرجعي

- Eric Evans، *Domain-Driven Design* (2003) — فصل Layered Architecture.
- Robert Martin، *Clean Architecture* (2017) — Dependency Rule.
- Spree Commerce — `spree_core` gem كنموذج Domain isolation معتمَد منذ 2008.
