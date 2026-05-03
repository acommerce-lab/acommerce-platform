#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
#  Runtime smoke test — تَشغيل Ejar.Api + Ejar.Web وفتح المتصفّح.
#
#  هذه الـ script تُشغَّل خارج الـ Claude Code sandbox (الذي يَقتل أيّ
#  بَرنامج يَربط منفذاً). نَفِّذها على جهازك المحلّيّ.
#
#  الاستعمال:
#    bash tools/checks/run-locally.sh        # full stack
#    bash tools/checks/run-locally.sh api    # backend only
#    bash tools/checks/run-locally.sh web    # frontend only
# ─────────────────────────────────────────────────────────────────────────────
set -e

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

start_api() {
  echo "▶ Ejar.Api → http://localhost:5300"
  dotnet run --project Apps/Ejar/Customer/Backend/Ejar.Api/Ejar.Api.csproj
}

start_web() {
  echo "▶ Ejar.Web → http://localhost:5113"
  echo "  افتح في المتصفّح:"
  echo "    /                 — الصفحة الرئيسيّة (legacy)"
  echo "    /properties       — kit page (Listings — alias لـ /listings)"
  echo "    /properties/{id}  — kit page (Listing detail)"
  echo "    /chat             — kit page (Chat inbox)"
  echo "    /notifications    — kit page (Notifications inbox)"
  echo "    /me               — kit page (Profile)"
  echo "    /plans            — kit page (Plans)"
  echo "    /support          — kit page (Support tickets)"
  echo "    /favorites        — kit page (Favorites)"
  echo "    /login            — kit page (Auth login)"
  echo
  dotnet run --project Apps/Ejar/Customer/Frontend/Ejar.Web/Ejar.Web.csproj
}

case "${1:-all}" in
  api) start_api ;;
  web) start_web ;;
  all)
    start_api &
    API_PID=$!
    trap "kill $API_PID 2>/dev/null || true" EXIT
    sleep 5
    start_web
    ;;
  *)  echo "usage: $0 [api|web|all]"; exit 1 ;;
esac
