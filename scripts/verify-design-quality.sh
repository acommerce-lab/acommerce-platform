#!/usr/bin/env bash
# Usage: ./scripts/verify-design-quality.sh
#
# LAYER 4 — Design-quality AGGREGATE metrics, scoped PER APP.
#
# A user only ever loads ONE app at a time, so measurements are meaningful
# only when summed across: this app's CSS + each referenced library's CSS
# (widgets + templates listed in the .csproj).
#
# What this layer uniquely answers (not covered elsewhere):
#   1. Spacing rhythm          — distinct padding/margin/gap values (≤ 20 per app)
#   2. Typography scale        — distinct font-size values         (≤ 10 per app)
#   3. Color diversity         — distinct colors                   (≤ 60 per app)
#   4. Icon size aggregate     — distinct icon sizes               (≤ 6 per app)
#   5. Widget usage top-10     — which widgets dominate the app
#   6. Per-page symmetry       — no mixed sm+lg button on same page
#   7. Container hierarchy     — page roots use approved layout
#
# Per-value correctness (is THIS value on the scale?) lives in Layer 3
# (verify-design-tokens.sh).  This layer is strictly aggregate diversity.

# Intentionally NOT `set -e` — grep exit-1 on no-match is expected here.
set -o pipefail
ROOT="${ROOT:-$(pwd)}"
WARN_SOFT=0
WARN_HARD=0

soft() { echo "  ⚠ $1"; WARN_SOFT=$((WARN_SOFT + 1)); }
hard() { echo "  ✗ $1"; WARN_HARD=$((WARN_HARD + 1)); }

section() { echo ""; echo "── $1 ──"; }

echo "═══════════════════════════════════════════════"
echo "   Design Quality Report — PER-APP visual rhythm & consistency"
echo "═══════════════════════════════════════════════"

# Resolve every frontend app and the CSS scope it actually ships.
# Scope = app's own CSS  +  each referenced libs/frontend/* CSS.
APPS=$(find "$ROOT/Apps" -name '*.csproj' -path '*/Frontend/*' \
    -not -path '*/bin/*' -not -path '*/obj/*' 2>/dev/null | sort)

# Build a comma-separated list of CSS search roots for a given csproj.
resolve_css_scope() {
    local csproj="$1"
    local app_dir
    app_dir=$(dirname "$csproj")

    # App's own CSS
    local scope="$app_dir"

    # Parse ProjectReference Include="..\..\..\libs\frontend\<Lib>\<Lib>.csproj"
    while IFS= read -r ref; do
        [ -z "$ref" ] && continue
        # Normalise backslashes, strip quotes
        local norm
        norm=$(echo "$ref" | tr '\\' '/' | grep -oE 'libs/frontend/[^"]+\.csproj')
        [ -z "$norm" ] && continue
        local lib_dir
        lib_dir="$ROOT/$(dirname "$norm")"
        [ -d "$lib_dir" ] && scope="$scope:$lib_dir"
    done < <(grep -E 'ProjectReference.*libs[/\\]frontend' "$csproj" 2>/dev/null)

    echo "$scope"
}

# Given a colon-separated list of directories, find every .css file under them
# (excluding bin/obj).  Prints one path per line.
collect_css_files() {
    local scope="$1"
    local dir
    for dir in $(echo "$scope" | tr ':' ' '); do
        find "$dir" -name '*.css' -not -path '*/bin/*' -not -path '*/obj/*' 2>/dev/null
    done | sort -u
}

# PER-APP METRIC COLLECTORS -----------------------------------------------
# Each returns a clean single integer (wc -l → always one number, even for empty input).
count_distinct_colors() {
    local files="$1"
    [ -z "$files" ] && { echo 0; return; }
    printf '%s\n' "$files" | xargs grep -hoE '#[0-9a-fA-F]{3,8}\b|rgb\([^)]+\)|rgba\([^)]+\)|hsl\([^)]+\)' 2>/dev/null |
        tr '[:upper:]' '[:lower:]' | sort -u | wc -l | tr -d ' '
}

count_distinct_spacings() {
    local files="$1"
    [ -z "$files" ] && { echo 0; return; }
    printf '%s\n' "$files" | xargs grep -hoE '(padding|margin|gap|row-gap|column-gap)(-[a-z]+)?:[[:space:]]*[0-9.]+(px|rem|em)' 2>/dev/null |
        grep -oE '[0-9.]+(px|rem|em)' | sort -u | wc -l | tr -d ' '
}

count_distinct_fontsizes() {
    local files="$1"
    [ -z "$files" ] && { echo 0; return; }
    printf '%s\n' "$files" | xargs grep -hoE 'font-size:[[:space:]]*[0-9.]+(px|rem|em)' 2>/dev/null |
        grep -oE '[0-9.]+(px|rem|em)' | sort -u | wc -l | tr -d ' '
}

count_distinct_icon_sizes() {
    local app_dir="$1"
    find "$app_dir" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
        -exec grep -hoE '<AcIcon[^>]+Size="[0-9]+"' {} \; 2>/dev/null |
        grep -oE 'Size="[0-9]+"' | sed 's/Size="//; s/"//' | sort -u | wc -l | tr -d ' '
}

# ═══════════════════════════════════════════════════════════════
# 1-4. PER-APP AGGREGATE METRICS
# ═══════════════════════════════════════════════════════════════
section "1-4. Per-app aggregate diversity (colors / spacing / font-size / icon-size)"
printf "  %-30s %-8s %-10s %-10s %-10s\n" "App" "Colors" "Spacings" "FontSizes" "IconSizes"
printf "  %-30s %-8s %-10s %-10s %-10s\n" "---" "------" "--------" "---------" "---------"

while IFS= read -r csproj; do
    [ -z "$csproj" ] && continue
    app_name=$(basename "$(dirname "$csproj")")
    scope=$(resolve_css_scope "$csproj")
    files=$(collect_css_files "$scope")

    colors=$(count_distinct_colors "$files")
    spacings=$(count_distinct_spacings "$files")
    fonts=$(count_distinct_fontsizes "$files")

    # Icon sizes used by the app's own razor files (not CSS)
    app_dir=$(dirname "$csproj")
    icon_sizes=$(count_distinct_icon_sizes "$app_dir")

    printf "  %-30s %-8s %-10s %-10s %-10s\n" "$app_name" "$colors" "$spacings" "$fonts" "$icon_sizes"

    # Well-known limits, PER APP (user-experienced):
    #   colors ≤ 60    spacings ≤ 20    font-sizes ≤ 10    icon sizes ≤ 6
    [ "$colors"   -gt 60 ] && hard "$app_name: too many colors ($colors > 60)"
    [ "$colors"   -gt 50 ] && [ "$colors" -le 60 ] && soft "$app_name: color diversity high ($colors)"
    [ "$spacings" -gt 20 ] && hard "$app_name: too many spacing values ($spacings > 20)"
    [ "$spacings" -gt 12 ] && [ "$spacings" -le 20 ] && soft "$app_name: spacing diversity moderate ($spacings)"
    [ "$fonts"    -gt 10 ] && hard "$app_name: too many font-sizes ($fonts > 10)"
    [ "$fonts"    -gt 8  ] && [ "$fonts" -le 10 ] && soft "$app_name: font-size diversity moderate ($fonts)"
    [ "$icon_sizes" -gt 6 ] && hard "$app_name: too many icon sizes ($icon_sizes > 6)"
done <<< "$APPS"

# ═══════════════════════════════════════════════════════════════
# 5. WIDGET USAGE DISTRIBUTION (top 10, global — inspection only)
# ═══════════════════════════════════════════════════════════════
section "5. Widget usage distribution (top 10 across all apps)"
find "$ROOT/Apps" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
    -exec grep -hoE '<Ac[A-Z][a-zA-Z]+' {} \; 2>/dev/null |
    sort | uniq -c | sort -rn | head -10 |
    awk '{ printf "  %4d  %s\n", $1, $2 }'

# ═══════════════════════════════════════════════════════════════
# 6. PER-PAGE HEIGHT CONSISTENCY (buttons must share size on same page)
# ═══════════════════════════════════════════════════════════════
section "6. Per-page sibling consistency"
MIXED_SIZES=$(
    find "$ROOT/Apps" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' | while read f; do
        if grep -q 'Size="sm"' "$f" && grep -q 'Size="lg"' "$f"; then
            echo "  ⚠ mixed sm+lg AcButton in: $(realpath --relative-to="$ROOT" "$f")"
        fi
    done
)
if [ -n "$MIXED_SIZES" ]; then
    echo "$MIXED_SIZES"
    soft "Some pages mix small and large button sizes (symmetry break)"
fi

# ═══════════════════════════════════════════════════════════════
# 7. CONTAINER HIERARCHY (every page wraps in approved container)
# ═══════════════════════════════════════════════════════════════
section "7. Container hierarchy (page root uses approved layout)"
APPROVED_ROOTS='(acs-page|acs-auth-page|acs-chat-page|acs-profile-page|acs-settings-page|adm-shell|<AcAuthGuard|<AcLoginPage|<AcCatalog|<AcSettingsPage|<AcProfilePage|<AcMessagesListPage|<AcChatPage|<AcNotificationsPage|<AcListingsPage|<AcVendorDashboard|<AcOwnerDashboard|<AcCatalogHome|<AcAdminMetricsPage|<AcAdminUsersPage|<AcAdminVendorsPage|<AcAdminOrdersPage|<AcPageHeader|<AcEmptyState|<AcLoadingState|<AcMapSearchPage|<AcCartPage|<AcOrderSuccessPage|<AcOfferDetailsPage|<AcCheckoutPage|<AcOrderListPage|<AcVendorOfferForm|<AcVendorSettings|<AcVendorSchedule|<AcMarketplaceHomePage|<AcListingExplorePage|<AcListingDetailsPage|<AcListingMapPage|<AcSearchSuggestionsPage|<AcNotificationsPage)'
BAD_PAGES=0
while IFS= read -r f; do
    base=$(basename "$f")
    case "$base" in
        MainLayout.razor|App.razor|Routes.razor|_Imports.razor|Error.razor) continue ;;
    esac
    content=$(head -40 "$f" | grep -E '<|class=')
    if ! echo "$content" | grep -qE "$APPROVED_ROOTS"; then
        soft "page doesn't start with approved root: $(realpath --relative-to="$ROOT" "$f")"
        BAD_PAGES=$((BAD_PAGES + 1))
    fi
done < <(find "$ROOT/Apps" -name '*.razor' -path '*/Pages/*' -not -path '*/bin/*' -not -path '*/obj/*')

# ═══════════════════════════════════════════════════════════════
# REPORT
# ═══════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════"
echo "   Summary"
echo "═══════════════════════════════════════════════"
echo "  Hard violations:  $WARN_HARD  (✗ — should fix)"
echo "  Soft violations:  $WARN_SOFT  (⚠ — consider fixing)"
echo ""
echo "Limits are per APP (user experience scope):"
echo "  colors ≤ 60   spacings ≤ 20   font-sizes ≤ 10   icon-sizes ≤ 6"

# Always exit 0 — this is a quality report, not a gate (can be tightened later)
exit 0
