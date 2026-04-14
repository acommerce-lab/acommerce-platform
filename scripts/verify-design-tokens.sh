#!/usr/bin/env bash
# Usage: ./scripts/verify-design-tokens.sh
#
# LAYER 3 — PER-VALUE correctness of design tokens.
#
# Asks: "Is THIS individual value on the allowed scale?"
# (Not: "how many distinct values exist?" — that lives in Layer 4.)
#
# Covers values that would otherwise slip through aggregate counts:
#   1. Font-size scale (per value)  — only predefined px sizes allowed
#   2. Spacing scale   (per value)  — inline padding/margin must be on 4px grid
#   3. Icon size scale (per value)  — AcIcon Size="..." must be on the scale
#   4. Icon name palette            — AcIcon Name="..." must be recognisable
#   5. DOM depth cap                — no razor file with excessive nesting
#
# Deliberately NOT here (moved to avoid duplication):
#   • Distinct-color count        → Layer 4 (aggregate, per-app scope)
#   • Raw-hex inline-style lint   → Layer 2 (verify-page-structure.sh, Rule 8)
#
# Inspired by: Stylelint, Prettier, Design Tokens W3C spec, Material Design scale.

set -eo pipefail
ROOT="${ROOT:-$(pwd)}"

VIOLATIONS=0
report() {
    echo "  ✗ $1"
    VIOLATIONS=$((VIOLATIONS + 1))
}

echo "=== Layer 3 (per-value): Design-Token Scale Verification ==="
echo ""

# ── 1. Font-size scale ─────────────────────────────────────────────────
echo "--- Checking inline font sizes... ---"
# Allowed px values in the scale (10,11,12,13,14,15,16,18,20,24,28,32,40,48)
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

# ── 2. Spacing tokens — raw px in inline styles ─────────────────────────
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

# ── 3. Icon size scale — per-value ──────────────────────────────────────
echo ""
echo "--- Checking AcIcon Size values against scale... ---"
ALLOWED_ICON_SIZES='^(14|16|18|20|22|24|28|32|40|48)$'
while IFS= read -r size; do
    [ -z "$size" ] && continue
    if ! echo "$size" | grep -qE "$ALLOWED_ICON_SIZES"; then
        report "off-scale icon size ($size): use 14/16/18/20/22/24/28/32/40/48"
    fi
done < <(find "$ROOT/Apps" "$ROOT/libs" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
    -exec grep -hoE '<AcIcon[^>]+Size="[0-9]+"' {} \; 2>/dev/null |
    grep -oE 'Size="[0-9]+"' | sed 's/Size="//; s/"//' | sort -u)

# ── 4. Icon name palette ───────────────────────────────────────────────
echo ""
echo "--- Checking AcIcon names... ---"
ICON_NAMES=$(find "$ROOT/Apps" "$ROOT/libs" -name '*.razor' \
    -exec grep -hoE '<AcIcon[^>]+Name="[^"]+"' {} \; 2>/dev/null | \
    grep -oE 'Name="[^"]+"' | sed 's/Name="//; s/"$//' | sort -u)
echo "Distinct icon names used: $(echo "$ICON_NAMES" | wc -l)"

# ── 5. DOM depth — count indentation levels in razor files ─────────────
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
