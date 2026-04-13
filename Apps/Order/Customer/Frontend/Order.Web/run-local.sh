#!/usr/bin/env bash
# Order — local dev runner. Builds and starts both services in the background,
# then prints the URLs. Run this from the repo root.
#
#   bash Apps/Order.Web/run-local.sh
#
# To stop:  fuser -k 5101/tcp 5701/tcp
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

echo "==> Building Order.Api + Order.Web"
dotnet build Apps/Order.Api/Order.Api.csproj -v q --nologo
dotnet build Apps/Order.Web/Order.Web.csproj -v q --nologo

echo "==> Starting Order.Api on http://localhost:5101"
( cd Apps/Order.Api && \
  ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --no-build > /tmp/order-api.log 2>&1 & )

echo "==> Starting Order.Web on http://localhost:5701"
( cd Apps/Order.Web && \
  ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --no-build --urls http://localhost:5701 > /tmp/order-web.log 2>&1 & )

# Wait for both to be reachable.
for i in 1 2 3 4 5 6 7 8 9 10; do
  sleep 1
  api=$(curl -sS -o /dev/null -w '%{http_code}' http://localhost:5101/health 2>/dev/null || echo 000)
  web=$(curl -sS -o /dev/null -w '%{http_code}' http://localhost:5701/         2>/dev/null || echo 000)
  if [ "$api" = "200" ] && [ "$web" = "200" ]; then
    echo
    echo "OK"
    echo "  API   : http://localhost:5101         (Swagger UI inside)"
    echo "  WEB   : http://localhost:5701         (Order app — phone shell)"
    echo "  Login : +966500000001  (the OTP is printed in /tmp/order-api.log)"
    echo
    echo "Stop with:  fuser -k 5101/tcp 5701/tcp"
    exit 0
  fi
done

echo "FAILED to bring services up. Last logs:"
echo "--- API ---"; tail -10 /tmp/order-api.log
echo "--- WEB ---"; tail -10 /tmp/order-web.log
exit 1
