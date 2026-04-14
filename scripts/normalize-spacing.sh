#!/usr/bin/env bash
# Normalize spacing to 4px grid. Target: 0, 4, 8, 12, 16, 20, 24, 32, 40, 48 px.

set -eo pipefail
ROOT="${ROOT:-$(pwd)}"
FILES=$(find "$ROOT/libs" "$ROOT/Apps" \
    \( -name '*.razor' -o -name '*.css' \) \
    -not -path '*/bin/*' -not -path '*/obj/*')

# Map of off-scale → on-scale (handles "N_px" before any non-digit boundary including "px")
# We only touch values immediately after padding:/margin:/gap: to avoid changing unrelated numbers.
declare -A REPL
REPL=(
    ["2px"]="4px"  ["3px"]="4px"  ["5px"]="4px"
    ["6px"]="8px"  ["9px"]="8px"
    ["10px"]="12px" ["11px"]="12px" ["13px"]="12px"
    ["14px"]="16px" ["15px"]="16px"
    ["18px"]="20px"
    ["22px"]="24px" ["26px"]="24px"
    ["28px"]="32px" ["36px"]="32px"
)

COUNT=0
for f in $FILES; do
    modified=0
    # Apply each replacement for each CSS property family
    for bad in "${!REPL[@]}"; do
        good="${REPL[$bad]}"
        for prop in padding padding-top padding-bottom padding-left padding-right padding-inline padding-block padding-inline-start padding-inline-end \
                    margin margin-top margin-bottom margin-left margin-right margin-inline margin-block margin-inline-start margin-inline-end \
                    gap row-gap column-gap; do
            # Use python for reliable in-place replacement
            if grep -q "${prop}:\s*${bad}\b" "$f" 2>/dev/null; then
                python3 -c "
import re, sys
with open('$f', 'r', encoding='utf-8') as fd: c = fd.read()
c2 = re.sub(r'${prop}:(\s*)${bad}\b', r'${prop}:\1${good}', c)
if c != c2:
    with open('$f', 'w', encoding='utf-8') as fd: fd.write(c2)
    sys.exit(0)
sys.exit(1)
" 2>/dev/null && modified=1
            fi
            # Also handle compound values like "padding: 6px 4px" where bad is first/second
            if grep -qE "${prop}:\s*${bad}\s+[0-9]+px" "$f" 2>/dev/null; then
                python3 -c "
import re, sys
with open('$f', 'r', encoding='utf-8') as fd: c = fd.read()
c2 = re.sub(r'${prop}:(\s*)${bad}(\s)', r'${prop}:\1${good}\2', c)
if c != c2:
    with open('$f', 'w', encoding='utf-8') as fd: fd.write(c2)
    sys.exit(0)
sys.exit(1)
" 2>/dev/null && modified=1
            fi
            if grep -qE "${prop}:\s*[0-9]+px\s+${bad}" "$f" 2>/dev/null; then
                python3 -c "
import re, sys
with open('$f', 'r', encoding='utf-8') as fd: c = fd.read()
c2 = re.sub(r'(${prop}:\s*[0-9]+px\s+)${bad}\b', r'\1${good}', c)
if c != c2:
    with open('$f', 'w', encoding='utf-8') as fd: fd.write(c2)
    sys.exit(0)
sys.exit(1)
" 2>/dev/null && modified=1
            fi
        done
    done
    if [ "$modified" = "1" ]; then COUNT=$((COUNT + 1)); fi
done

echo "Normalized spacing across $COUNT file(s)."
echo "Target scale: 0, 4, 8, 12, 16, 20, 24, 32, 40, 48 px"
