# --- مَرحَلَة البِناء (.NET 10 SDK رَسميّ مِن Microsoft) ---------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY platform-v1/ ./platform-v1/

RUN dotnet restore platform-v1/apps/V1.App/V1.App.csproj --nologo

# نَشر إنتاجيّ (Release). لا trimming لِأَنّ Blazor SSR
# يَستَخدِم reflection في بَعض الـ kits.
RUN dotnet publish platform-v1/apps/V1.App/V1.App.csproj \
        -c Release \
        -o /publish \
        --no-restore \
        --nologo

# --- مَرحَلَة التَّشغيل (ASP.NET Core 10 runtime رَسميّ) --------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
COPY --from=build /publish ./

# Hugging Face Spaces يَتَوَقَّع المَنفَذ 7860.
ENV ASPNETCORE_URLS=http://+:7860
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 7860

ENTRYPOINT ["dotnet", "V1.App.dll"]
