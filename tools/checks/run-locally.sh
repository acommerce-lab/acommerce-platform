#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
#  Runtime smoke test — تَشغيل Ejar.Api + Ejar.Web وفتح المتصفّح.
#
#  هذه الـ script تُشغَّل خارج الـ Claude Code sandbox (الذي يَقتل أيّ
#  بَرنامج يَربط منفذاً). نَفِّذها على جهازك المحلّيّ.
#
#  الاستعمال:
#    bash tools/checks/run-locally.sh        # api + web V1
#    bash tools/checks/run-locally.sh api    # backend only          → :5300
#    bash tools/checks/run-locally.sh web    # frontend V1 (legacy)  → :5113
#    bash tools/checks/run-locally.sh v2     # frontend V2 (kit-based) → :5114
#    bash tools/checks/run-locally.sh both   # api + web V1 + web V2
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

start_v2() {
  echo "▶ Ejar.Web.V2 (kit-based) → http://localhost:5114"
  echo "  افتح في المتصفّح:"
  echo "    /                — V2 home (kit widgets)"
  echo "    /chat            — AcChatInboxWidget"
  echo "    /chat/{id}       — AcChatRoomWidget (+ AcChatComposerWidget)"
  echo "    /notifications   — AcNotificationsInboxWidget"
  echo "    /dashboard       — composition: ChatUnreadBadge + NotificationsUnreadBadge"
  echo "    /properties      — AcListingExploreWidget"
  echo "    /me              — AcProfileWidget"
  echo "    /plans           — AcPlansWidget"
  echo "    /support         — AcTicketsWidget"
  echo "    /favorites       — AcFavoritesWidget"
  echo "    /login           — AcLoginWidget"
  echo
  dotnet run --project Apps/Ejar/Customer/Frontend/Ejar.Web.V2/Ejar.Web.V2.csproj
}

case "${1:-all}" in
  api)  start_api ;;
  web)  start_web ;;
  v2)   start_v2 ;;
  all)
    start_api &
    API_PID=$!
    trap "kill $API_PID 2>/dev/null || true" EXIT
    sleep 5
    start_web
    ;;
  both)
    start_api &
    API_PID=$!
    sleep 5
    start_web &
    WEB_PID=$!
    trap "kill $API_PID $WEB_PID 2>/dev/null || true" EXIT
    sleep 5
    start_v2
    ;;
  *)  echo "usage: $0 [api|web|v2|all|both]"; exit 1 ;;
esac
