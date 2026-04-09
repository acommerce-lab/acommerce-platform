#  Order.Api  —  multi-stage Dockerfile
#  Build:  docker build -t order-api -f Apps/Order.Api/Dockerfile .
#  Run:    docker run --rm -p 5101:5101 -v $(pwd)/data:/app/data order-api

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy ONLY the csprojs first so we get layer-cached restore
COPY Apps/Order.Api/Order.Api.csproj Apps/Order.Api/
COPY libs/ libs/

RUN dotnet restore Apps/Order.Api/Order.Api.csproj

# Now copy the source
COPY Apps/Order.Api/ Apps/Order.Api/

RUN dotnet publish Apps/Order.Api/Order.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5101
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5101

# SQLite database lives at ./data/order.db inside the container.
# Mount a host directory at /app/data to persist between runs.
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Order.Api.dll"]
