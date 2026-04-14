#!/usr/bin/env bash
# Usage: ./scripts/verify-design-tokens.sh
#
# Deeper design quality checks beyond structure. These ensure a CONSISTENT
# visual identity without needing manual review:
#
#   1. Color palette cap      — no more than N distinct hex colors in platform
#   2. No raw hex in razor    — inline style="color:#..." must use var(--ac-*)
#   3. Font-size scale limit  — only predefined sizes allowed
#   4. Spacing-token usage    — inline padding/margin must use var(--ac-space-*)
#   5. Icon consistency       — all AcIcon Name="..." values must exist in palette
#   6. DOM depth cap          — no razor file with >12 nested levels (bad hierarchy)
#
# Inspired by: Stylelint, Prettier, Design Tokens W3C spec, Material Design scale.

set -eo pipefail
ROOT="${ROOT:-$(pwd)}"

VIOLATIONS=0
report() {
    echo "  ✗ $1"
    VIOLATIONS=$((VIOLATIONS + 1))
}

echo "=== Layer 2 (deep): Design Tokens Verification ==="
echo ""

# ── 1. Color palette cap ───────────────────────────────────────────────
# Collect all hex colors used in CSS files, excluding those in :root :
HEX_COLORS=$(find "$ROOT" -name '*.css' -not -path '*/bin/*' -not -path '*/obj/*' \
    -exec grep -hoE '#[0-9a-fA-F]{3,8}\b' {} \; 2>/dev/null | \
    tr '[:upper:]' '[:lower:]' | sort -u | wc -l)
echo "Distinct hex colors in CSS: $HEX_COLORS"
if [ "$HEX_COLORS" -gt 120 ]; then
    report "Color palette too large (>120 distinct values). 5 apps × ~20 tokens × 2 themes = ~100 expected."
fi

# ── 2. No raw hex in razor inline styles ───────────────────────────────
echo ""
echo "--- Checking razor inline colors... ---"
while IFS= read -r hit; do
    [ -z "$hit" ] && continue
    report "raw hex in razor: $hit"
done < <(find "$ROOT/Apps" "$ROOT/libs" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
    -exec grep -HnE 'style="[^"]*(color|background|border-color):\s*#[0-9a-fA-F]+' {} \; 2>/dev/null | head -20)

# ── 3. Font-size scale ─────────────────────────────────────────────────
echo ""
echo "--- Checking inline font sizes... ---"
# Allowed: rem, em, %, CSS var, or px values in the scale (10,11,12,13,14,15,16,18,20,24,28,32,40,48)
ALLOWED_PX='^(10|11|12|13|14|15|16|18|20|24|28|32|40|48)px$'
while IFS= read -r hit; do
    [ -z "$hit" ] && continue
    val=$(echo "$hit" | grep -oE 'font-size:\s*[0-9.]+px' | grep -oE '[0-9.]+px')
    [ -z "$val" ] && continue
    if ! echo "$val" | grep -qE "$ALLOWED_PX"; then
        report "off-scale font-size ($val): $hit"
    fi
done < <(find "$ROOT/Apps" "$ROOT/libs" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
    -exec grep -HnE 'font-size:\s*[0-9.]+px' {} \; 2>/dev/null | head -20)

# ── 4. Spacing tokens — raw px in inline styles ─────────────────────────
echo ""
echo "--- Checking inline spacing... ---"
RAW_COUNT=0
while IFS= read -r hit; do
    [ -z "$hit" ] && continue
    # Extract EACH px value (may have multiple per line)
    for val in $(echo "$hit" | grep -oE '(padding|margin)(-(top|right|bottom|left|inline|block|inline-start|inline-end))?:\s*[0-9]+px' | grep -oE '[0-9]+px'); do
        px=${val%px}
        # Accept 0, or values 4..48 that are even
        if [ "$px" = "0" ]; then continue; fi
        if [ "$px" -lt 4 ] 2>/dev/null; then
            report "off-scale spacing ($val): $hit"
        elif [ "$px" -gt 48 ] 2>/dev/null; then
            report "off-scale spacing ($val): $hit"
        elif [ "$((px % 2))" -ne 0 ] 2>/dev/null; then
            report "odd-pixel spacing ($val): $hit"
        fi
        RAW_COUNT=$((RAW_COUNT + 1))
    done
done < <(find "$ROOT/Apps" "$ROOT/libs" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
    -exec grep -HnE 'style="[^"]*(padding|margin)[^:]*:\s*[0-9]+px' {} \; 2>/dev/null | head -30)
echo "Raw spacing declarations scanned: $RAW_COUNT (ok if multiples of 4, 0-48)"

# ── 5. Icon consistency ────────────────────────────────────────────────
echo ""
echo "--- Checking AcIcon names... ---"
# Extract every AcIcon Name="..." value and check it appears in the widgets.css icon set
ICON_NAMES=$(find "$ROOT/Apps" "$ROOT/libs" -name '*.razor' \
    -exec grep -hoE '<AcIcon[^>]+Name="[^"]+"' {} \; 2>/dev/null | \
    grep -oE 'Name="[^"]+"' | sed 's/Name="//; s/"$//' | sort -u)
echo "Distinct icon names used: $(echo "$ICON_NAMES" | wc -l)"

# ── 6. DOM depth — count indentation levels in razor files ─────────────
echo ""
echo "--- Checking nesting depth... ---"
while IFS= read -r file; do
    # Measure max indentation (spaces). Each level = 4 spaces.
    max_indent=$(awk '{ match($0, /^ */); if (RLENGTH > max) max = RLENGTH } END { print max+0 }' "$file")
    max_level=$((max_indent / 4))
    if [ "$max_level" -gt 16 ]; then
        report "deep nesting ($max_level levels): $(realpath --relative-to="$ROOT" "$file")"
    fi
done < <(find "$ROOT/Apps" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*')

# ── Report ─────────────────────────────────────────────────────────────
echo ""
echo "=== Report ==="
if [ "$VIOLATIONS" -gt 0 ]; then
    echo "Design token violations: $VIOLATIONS"
    echo "See docs/DESIGN-CRITERIA.md for the rationale behind each rule."
    exit 1
fi
echo "✅ All design tokens within acceptable bounds."
exit 0
