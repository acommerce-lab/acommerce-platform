#!/usr/bin/env bash
# Usage: ./scripts/verify-design-quality.sh
#
# THE DESIGN-QUALITY LAYER — separate from HTML/code hygiene checks.
# Measures visual rhythm, consistency, symmetry, and hierarchy.
#
# Unlike verify-page-structure.sh (code hygiene) or verify-css.sh (existence),
# this script quantifies DESIGN quality metrics:
#
#   1. Spacing rhythm     — how many distinct padding/margin values exist?
#   2. Typography scale   — how many distinct font-sizes?
#   3. Color diversity    — how many distinct colors?
#   4. Icon size consistency — are icons on a size scale?
#   5. Widget usage entropy — which widgets dominate? (healthier: AcButton, AcCard, AcField top)
#   6. Per-page consistency — on one page, do sibling elements share styling?
#   7. Container hierarchy  — every page root uses an approved layout class

set -eo pipefail
ROOT="${ROOT:-$(pwd)}"
WARN_SOFT=0
WARN_HARD=0

soft() { echo "  ⚠ $1"; WARN_SOFT=$((WARN_SOFT + 1)); }
hard() { echo "  ✗ $1"; WARN_HARD=$((WARN_HARD + 1)); }

section() { echo ""; echo "── $1 ──"; }

echo "═══════════════════════════════════════════════"
echo "   Design Quality Report — visual rhythm & consistency"
echo "═══════════════════════════════════════════════"

# ═══════════════════════════════════════════════════════════════
# 1. SPACING RHYTHM
# ═══════════════════════════════════════════════════════════════
section "1. Spacing rhythm (padding/margin/gap distinct values)"
# Collect every spacing value from all CSS + razor files
ALL_SPACING=$(
    {
        find "$ROOT/libs" "$ROOT/Apps" -name '*.css' -not -path '*/bin/*' -not -path '*/obj/*' \
            -exec grep -hoE '(padding|margin|gap|row-gap|column-gap)(-[a-z]+)?:\s*[0-9.]+(px|rem|em)' {} \; 2>/dev/null
        find "$ROOT/libs" "$ROOT/Apps" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
            -exec grep -hoE '(padding|margin|gap)(-[a-z]+)?:\s*[0-9.]+(px|rem|em)' {} \; 2>/dev/null
    } | grep -oE '[0-9.]+(px|rem|em)' | sort -u
)
COUNT=$(echo "$ALL_SPACING" | wc -l)
echo "$ALL_SPACING" | paste -sd' '
echo "Distinct spacing values: $COUNT"
if [ "$COUNT" -gt 20 ]; then hard "Too many distinct spacing values ($COUNT). Target ≤ 20, ideally: 4, 8, 12, 16, 20, 24, 32, 40, 48."
elif [ "$COUNT" -gt 12 ]; then soft "Spacing diversity is moderate ($COUNT). Ideal ≤ 12."
fi

# ═══════════════════════════════════════════════════════════════
# 2. TYPOGRAPHY SCALE
# ═══════════════════════════════════════════════════════════════
section "2. Typography scale (distinct font-size values)"
SIZES=$(
    {
        find "$ROOT/libs" "$ROOT/Apps" -name '*.css' -not -path '*/bin/*' -not -path '*/obj/*' \
            -exec grep -hoE 'font-size:\s*[0-9.]+(px|rem|em)' {} \; 2>/dev/null
        find "$ROOT/libs" "$ROOT/Apps" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
            -exec grep -hoE 'font-size:\s*[0-9.]+(px|rem|em)' {} \; 2>/dev/null
    } | grep -oE '[0-9.]+(px|rem|em)' | sort -u
)
COUNT=$(echo "$SIZES" | wc -l)
echo "$SIZES" | paste -sd' '
echo "Distinct font-sizes: $COUNT"
if [ "$COUNT" -gt 10 ]; then hard "Too many font-sizes ($COUNT). Target ≤ 8 on a scale (12, 14, 16, 18, 20, 24, 32, 40px)."
elif [ "$COUNT" -gt 8 ]; then soft "Font-size diversity is moderate ($COUNT)."
fi

# ═══════════════════════════════════════════════════════════════
# 3. COLOR DIVERSITY
# ═══════════════════════════════════════════════════════════════
section "3. Color diversity (distinct hex + rgb values)"
COLORS=$(
    find "$ROOT/libs" "$ROOT/Apps" -name '*.css' -not -path '*/bin/*' -not -path '*/obj/*' \
        -exec grep -hoE '#[0-9a-fA-F]{3,8}\b|rgb\([^)]+\)|rgba\([^)]+\)|hsl\([^)]+\)' {} \; 2>/dev/null |
    tr '[:upper:]' '[:lower:]' | sort -u
)
COUNT=$(echo "$COLORS" | wc -l)
echo "Distinct colors in CSS: $COUNT"
if [ "$COUNT" -gt 120 ]; then hard "Too many colors ($COUNT). Target ≤ 100 (5 apps × 20 tokens each incl dark mode)."
elif [ "$COUNT" -gt 100 ]; then soft "Color diversity is high ($COUNT)."
fi

# ═══════════════════════════════════════════════════════════════
# 4. ICON SIZE CONSISTENCY
# ═══════════════════════════════════════════════════════════════
section "4. Icon size consistency (AcIcon Size values)"
ICON_SIZES=$(
    find "$ROOT/libs" "$ROOT/Apps" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
        -exec grep -hoE '<AcIcon[^>]+Size="[0-9]+"' {} \; 2>/dev/null |
    grep -oE 'Size="[0-9]+"' | sed 's/Size="//; s/"//' | sort -u
)
COUNT=$(echo "$ICON_SIZES" | wc -l)
echo "Distinct icon sizes: $COUNT ($(echo $ICON_SIZES | tr '\n' ' '))"
# Accept the standard scale: 14, 16, 18, 20, 22, 24, 28, 32, 40, 48
ALLOWED_ICON_SIZES="^(14|16|18|20|22|24|28|32|40|48)$"
while IFS= read -r s; do
    [ -z "$s" ] && continue
    if ! echo "$s" | grep -qE "$ALLOWED_ICON_SIZES"; then
        soft "Off-scale icon size: $s (use 14/16/18/20/22/24/28/32/40/48)"
    fi
done <<< "$ICON_SIZES"
if [ "$COUNT" -gt 6 ]; then hard "Icon sizes too varied ($COUNT). Target ≤ 5 (16, 20, 24, 32, 48)."
fi

# ═══════════════════════════════════════════════════════════════
# 5. WIDGET USAGE DISTRIBUTION (entropy)
# ═══════════════════════════════════════════════════════════════
section "5. Widget usage distribution (top 10)"
find "$ROOT/Apps" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
    -exec grep -hoE '<Ac[A-Z][a-zA-Z]+' {} \; 2>/dev/null |
    sort | uniq -c | sort -rn | head -10 |
    awk '{ printf "  %4d  %s\n", $1, $2 }'

# ═══════════════════════════════════════════════════════════════
# 6. PER-PAGE HEIGHT CONSISTENCY (buttons/inputs must share height)
# ═══════════════════════════════════════════════════════════════
section "6. Per-page sibling consistency"
# Find pages where siblings have different widget sizes
# Currently: just check if any page uses mixed <AcButton Size="sm"> AND <AcButton Size="lg"> in same block
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
APPROVED_ROOTS='(acs-page|acs-auth-page|acs-chat-page|acs-profile-page|acs-settings-page|adm-shell|<AcAuthGuard|<AcLoginPage|<AcCatalog|<AcSettingsPage|<AcProfilePage|<AcMessagesListPage|<AcChatPage|<AcNotificationsPage|<AcListingsPage|<AcVendorDashboard|<AcOwnerDashboard|<AcCatalogHome|<AcAdminMetricsPage|<AcAdminUsersPage|<AcAdminVendorsPage|<AcAdminOrdersPage|<AcPageHeader|<AcEmptyState|<AcLoadingState|<AcMapSearchPage|<AcCartPage|<AcOrderSuccessPage|<AcOfferDetailsPage|<AcCheckoutPage|<AcOrderListPage|<AcVendorOfferForm|<AcVendorSettings|<AcVendorSchedule)'
BAD_PAGES=0
while IFS= read -r f; do
    # Skip Layout, App, Routes, Catalog, _Imports
    base=$(basename "$f")
    case "$base" in
        MainLayout.razor|App.razor|Routes.razor|_Imports.razor|Error.razor) continue ;;
    esac
    # Look at first 30 content lines (after @page/@using/@inject)
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
echo "Metric legend:"
echo "  Spacing values ≤ 12 → excellent rhythm"
echo "  Font-sizes     ≤ 8  → clear hierarchy"
echo "  Colors         ≤ 60 → restrained palette"
echo "  Icon sizes     ≤ 5  → consistent iconography"

# Always exit 0 — this is a quality report, not a gate (can be tightened later)
exit 0
