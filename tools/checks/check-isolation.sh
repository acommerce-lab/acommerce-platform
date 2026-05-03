#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
#  ACommerce Platform — Six isolation checks
#  يُثبت كَوْن الكيتس + القوالب + التطبيقات معزولة عن النطاق والبيانات.
#  كلّ فحص يَبحث عن استعمال حقيقيّ للـ token (لا comments) باستخدام
#  أنماط سياقيّة (cast/inject/using/namespace).
# ─────────────────────────────────────────────────────────────────────────────
set -u
PASS=0; FAIL=0
green() { printf '\033[32m✓\033[0m %s\n' "$1"; PASS=$((PASS+1)); }
red()   { printf '\033[31m✗\033[0m %s\n'   "$1"; FAIL=$((FAIL+1)); }
header(){ printf '\n\033[1m%s\033[0m\n' "$1"; }

# strip Razor comments (@* … *@) + XML-doc lines (///) قبل grep
strip_comments() {
  sed -E 's|@\*[^*]*\*@||g; /^[[:space:]]*\/\/\//d;'
}

# ─── الفحص ١: لا MarkupString cast/parameter في kit page أو template ──
header "[1/6] No MarkupString usage in kit pages/templates (XSS guard)"
hits=""
for f in $(find libs/kits/*/Frontend/Customer libs/templates \( -name "*.razor" -o -name "*.cs" \) 2>/dev/null | grep -v "/obj/\|/bin/"); do
  out=$(strip_comments < "$f" | grep -nE '\(MarkupString\)|<MarkupString\b|new MarkupString' || true)
  if [ -n "$out" ]; then hits+="$f: $out"$'\n'; fi
done
if [ -z "$hits" ]; then green "no MarkupString cast/parameter in kit pages or templates"
else red "MarkupString usage:"; echo "$hits" | sed 's/^/    /'; fi

# ─── الفحص ٢: لا @inject HttpClient في kit page ───────────────────────
header "[2/6] No HttpClient injection in kit pages (data-isolation)"
hits=""
for f in $(find libs/kits/*/Frontend/Customer \( -name "*.razor" -o -name "*.cs" \) 2>/dev/null | grep -v "/obj/\|/bin/"); do
  out=$(strip_comments < "$f" | grep -nE '@inject[[:space:]]+HttpClient|^[[:space:]]*using[[:space:]]+System\.Net\.Http[[:space:]]*;' || true)
  if [ -n "$out" ]; then hits+="$f: $out"$'\n'; fi
done
if [ -z "$hits" ]; then green "kit pages do not @inject HttpClient or import System.Net.Http"
else red "HttpClient leakage:"; echo "$hits" | sed 's/^/    /'; fi

# ─── الفحص ٣: kit pages لا تَستهلك app entities ────────────────────────
header "[3/6] No app entity types in kit pages (interface-only deps)"
hits=""
for f in $(find libs/kits/*/Frontend/Customer \( -name "*.razor" -o -name "*.cs" \) 2>/dev/null | grep -v "/obj/\|/bin/"); do
  out=$(strip_comments < "$f" | grep -nE '^[[:space:]]*using[[:space:]]+(Ejar|Order|Ashare)\b|@using[[:space:]]+(Ejar|Order|Ashare)\b' || true)
  if [ -n "$out" ]; then hits+="$f: $out"$'\n'; fi
done
if [ -z "$hits" ]; then green "kit pages depend only on kit interfaces (IListing, IChatMessage, …)"
else red "app entity refs:"; echo "$hits" | sed 's/^/    /'; fi

# ─── الفحص ٤: كلّ IXxxStore له binding في AddEjarCustomer ─────────────
header "[4/6] All kit IXxxStore interfaces wired in EjarCustomerHost"
expected=(IAuthStore IListingsStore IChatStore INotificationsStore IProfileStore ISubscriptionsStore ISupportStore IFavoritesStore)
host="Apps/Ejar/Customer/Shared/Ejar.Customer.UI/ClientHost/EjarCustomerHost.cs"
missing=0
for s in "${expected[@]}"; do
  if grep -qE "\.Use<.*\b$s\b" "$host"; then :; else red "binding missing for $s"; missing=$((missing+1)); fi
done
if [ "$missing" -eq 0 ]; then green "8/8 kit stores wired (Auth, Listings, Chat, Notifications, Profile, Subscriptions, Support, Favorites)"; fi

# ─── الفحص ٥: shims خاليتان من kit/domain imports ──────────────────────
header "[5/6] Shims contain only platform glue (no domain imports)"
for shim in Apps/Ejar/Customer/Frontend/Ejar.Web/Program.cs Apps/Ejar/Customer/Frontend/Ejar.Maui/MauiProgram.cs; do
  out=$(strip_comments < "$shim" | grep -nE '^[[:space:]]*using[[:space:]]+(ACommerce\.Kits|Ejar\.Domain)' | grep -v "Ejar.Customer.UI\|Versions" || true)
  if [ -z "$out" ]; then green "$shim — no kit/domain imports"
  else red "$shim leaks kit/domain imports:"; echo "$out" | sed 's/^/    /'; fi
done

# ─── الفحص ٦: kit Frontend.Customer لا تَذكُر Ejar (portability) ──────
header "[6/6] Cross-app reusability: kit Frontend.Customer is app-agnostic"
hits=""
for f in $(find libs/kits/*/Frontend/Customer \( -name "*.razor" -o -name "*.cs" -o -name "*.csproj" \) 2>/dev/null | grep -v "/obj/\|/bin/"); do
  out=$(strip_comments < "$f" | grep -nE '^[[:space:]]*using[[:space:]]+Ejar|@using[[:space:]]+Ejar|<ProjectReference[^>]*Ejar' || true)
  if [ -n "$out" ]; then hits+="$f: $out"$'\n'; fi
done
if [ -z "$hits" ]; then green "kit Frontend.Customer projects ⊥ Ejar — droppable into any app"
else red "Ejar leaked into kit:"; echo "$hits" | sed 's/^/    /'; fi

# ─── الخلاصة ──────────────────────────────────────────────────────────
echo
printf '\033[1mResult:\033[0m %d passed / %d failed\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] && exit 0 || exit 1
