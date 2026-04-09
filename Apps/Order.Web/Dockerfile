#  Order.Web  —  multi-stage Dockerfile
#  Build:  docker build -t order-web -f Apps/Order.Web/Dockerfile .
#  Run:    docker run --rm -p 5701:5701 \
#               -e OrderApi__BaseUrl=http://order-api:5101 \
#               order-web

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Apps/Order.Web/Order.Web.csproj Apps/Order.Web/
COPY libs/ libs/
COPY clients/ clients/

RUN dotnet restore Apps/Order.Web/Order.Web.csproj

COPY Apps/Order.Web/ Apps/Order.Web/

RUN dotnet publish Apps/Order.Web/Order.Web.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5701
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5701

ENTRYPOINT ["dotnet", "Order.Web.dll"]
