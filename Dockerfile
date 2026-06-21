# --- مَرحَلَة البِناء ---------------------------------------------------------
# نَستَخدِم Ubuntu 24.04 لِأَنّ Microsoft الرَّسميَّة (mcr) قَد لا تَكون
# مُتاحَة في كُلّ بَيئات HF بِنَفس الإصدار. apt يَستَخدِم noble
# (10.0.109~24.04.1) — نَفس المَجموعَة الَّتي يَعمَل بِها مُطَوِّرونا.
FROM ubuntu:24.04 AS build
ARG DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
      -o Dir::Etc::sourcelist="sources.list" \
      -o Dir::Etc::sourceparts="-" \
      -o APT::Get::List-Cleanup="0" \
 && apt-get install -y --no-install-recommends \
        dotnet-sdk-10.0 \
        ca-certificates \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# طَبَقَة restore مُنفَصِلَة: نَنسَخ المِحلول + كل csproj فَقَط لِتَخزين
# الـ NuGet cache في طَبَقَة dockerlayer لا تَتَغَيَّر إلّا عِندَ تَغَيُّر التَّبَعِيّات.
COPY ACommerce.Platform.sln ./
COPY platform-v1/ ./platform-v1/

RUN dotnet restore platform-v1/apps/V1.App/V1.App.csproj --nologo

# نَشر إنتاجيّ مُحَسَّن (Release + Trimmed False لِأَنّ Blazor SSR
# يَستَخدِم reflection في بَعض الـ kits).
RUN dotnet publish platform-v1/apps/V1.App/V1.App.csproj \
        -c Release \
        -o /publish \
        --no-restore \
        --nologo

# --- مَرحَلَة التَّشغيل --------------------------------------------------------
# Runtime فَقَط (أَصغَر) — لا نَحتاج SDK في الصورة النِّهائيَّة.
FROM ubuntu:24.04 AS runtime
ARG DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
      -o Dir::Etc::sourcelist="sources.list" \
      -o Dir::Etc::sourceparts="-" \
      -o APT::Get::List-Cleanup="0" \
 && apt-get install -y --no-install-recommends \
        aspnetcore-runtime-10.0 \
        ca-certificates \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /publish ./

# Hugging Face Spaces يَتَوَقَّع 7860. اِجعَله افتراضيّاً قابِلاً لِلتَّجاوُز
# عِندَ تَشغيل الحاوية مَحَلِّيّاً (مَثَلاً 5050).
ENV ASPNETCORE_URLS=http://+:7860
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_USE_POLLING_FILE_WATCHER=false

EXPOSE 7860

ENTRYPOINT ["dotnet", "V1.App.dll"]
