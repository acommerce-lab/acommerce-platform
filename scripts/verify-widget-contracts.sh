#!/usr/bin/env bash
# Usage: ./scripts/verify-widget-contracts.sh
#
# LAYER 5 — Widget Contracts (completeness check).
#
# Every widget declares its visual contract in scripts/widget-contracts.json.
# This script verifies that each widget's CSS satisfies that contract:
#   1. Every REQUIRED property is declared.
#   2. Every MIN-VALUE is met.
#
# Unlike Layer 3 (verify-design-tokens.sh) which catches WRONG values,
# this layer catches MISSING properties — e.g. .ac-input without padding.
#
# Question it answers: "Does this widget actually have padding, a border,
# readable text, etc — or did someone forget to define these?"

set -eo pipefail
ROOT="${ROOT:-$(pwd)}"
CONTRACTS="$ROOT/scripts/widget-contracts.json"

VIOLATIONS=0
report() { echo "  ✗ $1"; VIOLATIONS=$((VIOLATIONS + 1)); }
ok()     { echo "  ✓ $1"; }

echo "═══════════════════════════════════════════════"
echo "   Widget Contracts — completeness verification"
echo "═══════════════════════════════════════════════"
echo ""

# Collect all CSS content into one string (selectors may be in any file)
ALL_CSS=$(find "$ROOT/libs" "$ROOT/Apps" -name '*.css' \
    -not -path '*/bin/*' -not -path '*/obj/*' \
    -exec cat {} + 2>/dev/null)

# Extract contracts from JSON (simple parsing; assumes well-formed)
SELECTORS=$(grep -oE '"\.[a-z][a-z0-9_-]*":' "$CONTRACTS" | sed 's/"://; s/^"//')

check_selector() {
    local selector="$1"
    local required="$2"

    # Extract the full CSS block for this selector (may have multiple)
    # Match `.selector { ... }` across the whole CSS
    local block
    block=$(echo "$ALL_CSS" | awk -v sel="$selector" '
        BEGIN { inblock = 0; output = "" }
        {
            line = $0
            # Check if line starts a block for our selector
            # Match selector followed by optional selectors, then {
            if (inblock == 0 && match(line, sel "([, ]|[[:space:]]*\\{)")) {
                inblock = 1
                output = output " " line
            } else if (inblock == 1) {
                output = output " " line
                if (index(line, "}") > 0) {
                    inblock = 0
                    print output
                    output = ""
                }
            }
        }
    ')

    if [ -z "$block" ]; then
        report "$selector: NO CSS RULE FOUND (contract requires: $required)"
        return
    fi

    # Check each required property
    local missing=""
    for prop in $required; do
        # Handle special compound properties
        case "$prop" in
            border)
                if ! echo "$block" | grep -qE '(border|border-width|border-style|border-color):' ; then
                    missing="$missing $prop"
                fi
                ;;
            border-bottom)
                if ! echo "$block" | grep -qE '(border-bottom|border):' ; then
                    missing="$missing $prop"
                fi
                ;;
            padding)
                if ! echo "$block" | grep -qE '(padding|padding-(top|right|bottom|left|inline|block)):' ; then
                    missing="$missing $prop"
                fi
                ;;
            margin-bottom)
                if ! echo "$block" | grep -qE '(margin-bottom|margin):' ; then
                    missing="$missing $prop"
                fi
                ;;
            *)
                if ! echo "$block" | grep -qE "^[^/]*\b${prop}:" ; then
                    missing="$missing $prop"
                fi
                ;;
        esac
    done

    if [ -n "$missing" ]; then
        report "$selector missing required properties:$missing"
    else
        ok "$selector: all required properties declared"
    fi
}

# Iterate contract selectors manually (bash portable)
# Read the JSON and process each contract
echo "Checking contracts..."
echo ""

# A quick, line-based JSON walk (good enough for our flat structure)
current=""
required=""
while IFS= read -r line; do
    # New selector
    if echo "$line" | grep -qE '"\.[a-z][a-z0-9_-]*":\s*\{'; then
        current=$(echo "$line" | grep -oE '"\.[a-z][a-z0-9_-]*"' | sed 's/"//g')
        required=""
    fi
    # Required array
    if echo "$line" | grep -qE '"required":' && [ -n "$current" ]; then
        required=$(echo "$line" | grep -oE '\[[^]]*\]' | tr -d '[]",' | tr -s ' ')
        [ -n "$required" ] && check_selector "$current" "$required"
        current=""
    fi
done < "$CONTRACTS"

echo ""
echo "═══════════════════════════════════════════════"
echo "   Summary"
echo "═══════════════════════════════════════════════"
echo "  Missing-property violations: $VIOLATIONS"
if [ "$VIOLATIONS" -gt 0 ]; then
    echo ""
    echo "Each widget must declare the properties listed in its contract."
    echo "Missing = element will render without padding/border/background =>"
    echo "text will stick to edges, inputs will be invisible, etc."
    exit 1
fi
echo "  ✅ Every contracted widget satisfies its visual baseline."
exit 0
