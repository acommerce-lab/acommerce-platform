#!/usr/bin/env bash
# Usage: ./scripts/verify-css.sh
# Scans all .razor files for class="..." usage, all .css files for .class
# definitions, reports undefined classes (exits 1 if any).

set -eo pipefail

ROOT="${ROOT:-$(pwd)}"
TMP=$(mktemp -d)
trap "rm -rf $TMP" EXIT

echo "=== Extracting classes from .razor files ==="
# Collect class="..." attributes, split on whitespace, filter literals (no @)
find "$ROOT" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
  -exec grep -hoE 'class="[^"]+"' {} \; 2>/dev/null |
  sed 's/class="//; s/"$//' |
  tr ' \t' '\n\n' |
  grep -E '^[a-z][a-z0-9_-]*$' |
  sort -u > "$TMP/used.txt"

# Also extract literals inside razor ternary that appear inside class="..."
# Be strict: only pick up strings inside class="@(...)" expressions
find "$ROOT" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
  -exec grep -hoE 'class="[^"]*@\([^)]+\)[^"]*"' {} \; 2>/dev/null |
  grep -oE '"[a-z][a-z0-9_-]*"' |
  tr -d '"' |
  grep -E '^[a-z][a-z0-9_-]*$' |
  sort -u >> "$TMP/used.txt"
sort -u -o "$TMP/used.txt" "$TMP/used.txt"

echo "=== Extracting class selectors from .css files ==="
find "$ROOT" -name '*.css' -not -path '*/bin/*' -not -path '*/obj/*' \
  -exec grep -hoE '\.[a-z][a-z0-9_-]*' {} \; 2>/dev/null |
  sed 's/^\.//' |
  sort -u > "$TMP/defined.txt"

echo "=== Ignore list (built-in / known false positives) ==="
cat > "$TMP/ignore.txt" <<'EOF'
valid
invalid
modified
sr-only
visually-hidden
active
disabled
hidden
loading
light
dark
success
error
warning
danger
info
primary
secondary
unread
mine
recommended
all
pending
confirmed
ready
cancelled
closed
open
delivered
preparing
ar
en
is
null
true
false
offers
vendors
list
map
r
EOF

# Classes used but not defined
comm -23 "$TMP/used.txt" "$TMP/defined.txt" | \
  grep -vxF -f "$TMP/ignore.txt" > "$TMP/undefined.txt" || true

# Classes defined but not used
comm -13 "$TMP/used.txt" "$TMP/defined.txt" > "$TMP/unused.txt" || true

USED_CNT=$(wc -l < "$TMP/used.txt")
DEFINED_CNT=$(wc -l < "$TMP/defined.txt")
UNDEF_CNT=$(wc -l < "$TMP/undefined.txt")
UNUSED_CNT=$(wc -l < "$TMP/unused.txt")

echo ""
echo "=== CSS Class Verification Report ==="
echo "Classes used:    $USED_CNT"
echo "Classes defined: $DEFINED_CNT"
echo "UNDEFINED:       $UNDEF_CNT (build breakers)"
echo "Unused:          $UNUSED_CNT (warnings)"
echo ""

if [ "$UNDEF_CNT" -gt 0 ]; then
  echo "=== ✗ UNDEFINED CLASSES (used in .razor but not defined in any .css) ==="
  while IFS= read -r cls; do
    echo "  ✗ .$cls"
    # Find files that use this class (limit 3)
    grep -l "class=\"[^\"]*\b$cls\b" \
      $(find "$ROOT" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*') 2>/dev/null | \
      head -3 | sed "s|$ROOT/|      used in: |"
  done < "$TMP/undefined.txt"
  exit 1
fi

echo "✅ All used classes are defined."
exit 0
