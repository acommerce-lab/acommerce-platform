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
echo "=== Scanning for malformed CSS declarations ==="
# Catch declarations that are a literal value followed by `;` on a line of
# their own inside a `{ ... }` block — the classic "forgot to type the
# property name" bug (e.g. `16px;` instead of `padding: 16px;`).  We only
# flag lines that: (1) are inside a block, (2) contain a `;`, (3) have no
# `:`, and (4) don't end in `,` and the previous non-blank line didn't end
# in `,` (which would make them selector-list or multi-line-value
# continuations).
MALFORMED=$(mktemp)
find "$ROOT" -name '*.css' -not -path '*/bin/*' -not -path '*/obj/*' | while read css; do
    # First pass: strip /* ... */ comments, preserve line numbers.
    python3 - "$css" <<'PY'
import sys, re
path = sys.argv[1]
text = open(path, encoding='utf-8').read()
# strip /* … */ while keeping newlines so line numbers stay stable
def strip_block(m):
    return re.sub(r'[^\n]', ' ', m.group(0))
clean = re.sub(r'/\*.*?\*/', strip_block, text, flags=re.S)
lines = clean.split('\n')
depth = 0
prev_ended_cont = False
for i, line in enumerate(lines, 1):
    stripped = line.strip()
    # Count braces BEFORE logic (handles { and } on same line)
    for ch in stripped:
        if ch == '{': depth += 1
        elif ch == '}': depth -= 1
    if depth <= 0 or not stripped:
        prev_ended_cont = False
        continue
    # Skip @-rules, selector-start lines that end in {
    if stripped.startswith('@') or stripped.endswith('{'):
        prev_ended_cont = False
        continue
    # Continuation tracking — previous line ended in "," or open "(" without close
    if prev_ended_cont:
        # Does this line also continue?
        prev_ended_cont = stripped.endswith(',') or (stripped.count('(') > stripped.count(')'))
        continue
    if stripped.endswith(',') or (stripped.count('(') > stripped.count(')')):
        prev_ended_cont = True
        continue
    # Must contain a `;` to be a declaration terminator; must NOT contain `:`
    if ';' not in stripped: continue
    if ':' in stripped: continue
    # Ignore lines that look like partial selectors (contain { or })
    if '{' in stripped or '}' in stripped: continue
    print(f"{path}:{i}:{stripped}")
PY
done > "$MALFORMED"

MALFORMED_CNT=$(wc -l < "$MALFORMED")
if [ "$MALFORMED_CNT" -gt 0 ]; then
    echo "  ✗ $MALFORMED_CNT malformed CSS declaration(s) (value without property name):"
    head -10 "$MALFORMED" | sed "s|$ROOT/|    |"
fi
rm -f "$MALFORMED"

echo ""
echo "=== CSS Class Verification Report ==="
echo "Classes used:    $USED_CNT"
echo "Classes defined: $DEFINED_CNT"
echo "UNDEFINED:       $UNDEF_CNT (build breakers)"
echo "Malformed decls: $MALFORMED_CNT (build breakers)"
echo "Unused:          $UNUSED_CNT (warnings)"
echo ""

if [ "$MALFORMED_CNT" -gt 0 ]; then
    echo "Fix malformed declarations: every declaration must be property:value;"
    exit 1
fi

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
