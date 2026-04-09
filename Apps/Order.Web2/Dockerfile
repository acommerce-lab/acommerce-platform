#  Order.Web2  —  multi-stage Dockerfile
#  Build:  docker build -t order-web2 -f Apps/Order.Web2/Dockerfile .
#  Run:    docker run --rm -p 5701:5701 \
#               -e OrderApi__BaseUrl=http://order-api2:5101 \
#               order-web2

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Apps/Order.Web2/Order.Web2.csproj Apps/Order.Web2/
COPY libs/ libs/
COPY clients/ clients/

RUN dotnet restore Apps/Order.Web2/Order.Web2.csproj

COPY Apps/Order.Web2/ Apps/Order.Web2/

RUN dotnet publish Apps/Order.Web2/Order.Web2.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5701
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5701

ENTRYPOINT ["dotnet", "Order.Web2.dll"]
