#!/usr/bin/env bash
# Normalize font-sizes across entire codebase to 8-step scale.
#
# Target scale (Material Type / TailwindCSS inspired):
#   0.75rem   = 12px — xs (captions, labels)
#   0.875rem  = 14px — sm (secondary text)
#   1rem      = 16px — base (body)
#   1.125rem  = 18px — md (emphasis)
#   1.25rem   = 20px — lg (sub-headings)
#   1.5rem    = 24px — xl (headings)
#   2rem      = 32px — 2xl (page titles)
#   3rem      = 48px — 3xl (hero)

set -eo pipefail
ROOT="${ROOT:-$(pwd)}"

FILES=$(find "$ROOT/libs" "$ROOT/Apps" \
    \( -name '*.razor' -o -name '*.css' \) \
    -not -path '*/bin/*' -not -path '*/obj/*')

TMP=$(mktemp)
trap "rm -f $TMP" EXIT

# Mapping rules (source → target rem value)
cat > "$TMP" <<'EOF'
s|font-size:\s*0\.62rem|font-size:0.75rem|g
s|font-size:\s*0\.65rem|font-size:0.75rem|g
s|font-size:\s*0\.68rem|font-size:0.75rem|g
s|font-size:\s*0\.72rem|font-size:0.75rem|g
s|font-size:\s*0\.74rem|font-size:0.75rem|g
s|font-size:\s*0\.76rem|font-size:0.75rem|g
s|font-size:\s*0\.78rem|font-size:0.875rem|g
s|font-size:\s*0\.8rem|font-size:0.875rem|g
s|font-size:\s*0\.82rem|font-size:0.875rem|g
s|font-size:\s*0\.84rem|font-size:0.875rem|g
s|font-size:\s*0\.85rem|font-size:0.875rem|g
s|font-size:\s*0\.86rem|font-size:0.875rem|g
s|font-size:\s*0\.88rem|font-size:0.875rem|g
s|font-size:\s*0\.9rem|font-size:0.875rem|g
s|font-size:\s*0\.92rem|font-size:0.875rem|g
s|font-size:\s*0\.94rem|font-size:1rem|g
s|font-size:\s*0\.95rem|font-size:1rem|g
s|font-size:\s*1\.02rem|font-size:1rem|g
s|font-size:\s*1\.05rem|font-size:1.125rem|g
s|font-size:\s*1\.1rem|font-size:1.125rem|g
s|font-size:\s*1\.15rem|font-size:1.125rem|g
s|font-size:\s*1\.2rem|font-size:1.25rem|g
s|font-size:\s*1\.25rem|font-size:1.25rem|g
s|font-size:\s*1\.3rem|font-size:1.25rem|g
s|font-size:\s*1\.35rem|font-size:1.5rem|g
s|font-size:\s*1\.4rem|font-size:1.5rem|g
s|font-size:\s*1\.45rem|font-size:1.5rem|g
s|font-size:\s*1\.6rem|font-size:1.5rem|g
s|font-size:\s*1\.75rem|font-size:2rem|g
s|font-size:\s*1\.8rem|font-size:2rem|g
s|font-size:\s*2\.2rem|font-size:2rem|g
s|font-size:\s*2\.4rem|font-size:3rem|g
s|font-size:\s*2\.5rem|font-size:3rem|g
s|font-size:\s*2\.6rem|font-size:3rem|g
s|font-size:\s*2\.8rem|font-size:3rem|g
s|font-size:\s*3\.2rem|font-size:3rem|g
s|font-size:\s*4rem|font-size:3rem|g
s|font-size:\s*6rem|font-size:3rem|g
s|font-size:\s*14px|font-size:0.875rem|g
s|font-size:\s*15px|font-size:1rem|g
EOF

COUNT=0
for f in $FILES; do
    if grep -qE 'font-size:' "$f" 2>/dev/null; then
        if sed -i -E -f "$TMP" "$f" 2>/dev/null || sed -i "" -E -f "$TMP" "$f" 2>/dev/null; then
            COUNT=$((COUNT + 1))
        fi
    fi
done

echo "Normalized font-sizes across $COUNT file(s)."
echo "Target scale: 0.75 / 0.875 / 1 / 1.125 / 1.25 / 1.5 / 2 / 3 rem"
