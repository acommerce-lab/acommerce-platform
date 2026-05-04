#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
#  Layer 6 — runtime visual verification on Ejar.Web.V2
#  يُثبت أنّ التَطبيق يَعمل فعلاً ويَلبس الـ Ledger Theme.
#
#  المتطلّبات:
#    1. Chrome for Testing في /opt/browsers/chrome-linux64/chrome
#       (راجع docs/DOTNET-SETUP.md → "تثبيت Chrome for Testing")
#    2. Playwright npm package: cd /tmp/shot-deps && npm install playwright
#    3. dotnet 10.0.x
#
#  الناتج: tools/checks/screenshots/v2-*.png  (mobile viewport)
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

PORT=5114
URL="http://localhost:$PORT"
LOG=/tmp/ejar-v2.log

# 1. ابنِ
echo "▶ build Ejar.Web.V2"
dotnet build Apps/Ejar/Customer/Frontend/Ejar.Web.V2/Ejar.Web.V2.csproj \
  --nologo -v quiet >/dev/null

# 2. شَغِّل في الخلفيّة
echo "▶ start V2 on :$PORT"
dotnet run --project Apps/Ejar/Customer/Frontend/Ejar.Web.V2/Ejar.Web.V2.csproj \
  --no-build --urls "$URL" > "$LOG" 2>&1 &
V2_PID=$!
trap "kill $V2_PID 2>/dev/null || true; pkill -f Ejar.Web.V2 2>/dev/null || true" EXIT

# 3. انتظر الإقلاع
for i in $(seq 1 20); do
  if curl -sf "$URL/" >/dev/null 2>&1; then break; fi
  sleep 1
done
if ! curl -sf "$URL/" >/dev/null 2>&1; then
  echo "✗ V2 failed to start"; tail -30 "$LOG"; exit 1
fi
echo "✓ V2 listening (HTTP 200 on /)"

# 4. خُذ لقطات
mkdir -p tools/checks/screenshots
URL="$URL" \
  OUT="$ROOT/tools/checks/screenshots" \
  node /tmp/shot.mjs

echo
echo "✓ done — see tools/checks/screenshots/v2-*.png"
