#!/usr/bin/env bash
# verify-backend-mutations.sh
# Ensures no controller calls _repo.AddAsync / UpdateAsync / DeleteAsync / SaveChangesAsync
# directly — all mutations must be inside Entry.Create(...).Execute(...).Build() bodies.
# Exit 1 if violations found.

set -eo pipefail

ROOT="${ROOT:-$(pwd)}"
TARGET="${1:-$ROOT}"
VIOLATIONS=0
VIOLATIONS_FILE=$(mktemp)
trap "rm -f $VIOLATIONS_FILE" EXIT

report() {
    echo "  ✗ [mutation] $1:$2" >> "$VIOLATIONS_FILE"
    echo "      $3" >> "$VIOLATIONS_FILE"
    VIOLATIONS=$((VIOLATIONS + 1))
}

# Patterns that must not appear directly in controller methods (outside .Execute lambdas)
FORBIDDEN_PATTERNS=(
    '_repo\.AddAsync\b'
    '_repo\.UpdateAsync\b'
    '_repo\.DeleteAsync\b'
    '_repository\.AddAsync\b'
    '_repository\.UpdateAsync\b'
    '_repository\.DeleteAsync\b'
    'context\.SaveChangesAsync\b'
    '_context\.SaveChangesAsync\b'
    '_db\.SaveChangesAsync\b'
)

while IFS= read -r file; do
    lineno=0
    while IFS= read -r line; do
        lineno=$((lineno + 1))
        # Skip lines inside lambda/closure (heuristic: indented inside Execute)
        # Simple check: flag any direct call not inside a lambda arrow
        for pat in "${FORBIDDEN_PATTERNS[@]}"; do
            if echo "$line" | grep -qE "$pat" 2>/dev/null; then
                # Allow if the same line has => (it's inside a lambda)
                if ! echo "$line" | grep -qE '=>\s*(await\s+)?_' 2>/dev/null; then
                    report "$file" "$lineno" "$line"
                fi
            fi
        done
    done < "$file"
done < <(find "$TARGET" -path '*/Controllers/*.cs' -not -path '*/obj/*' -not -path '*/bin/*')

echo "=== verify-backend-mutations ==="
if [ "$VIOLATIONS" -gt 0 ]; then
    cat "$VIOLATIONS_FILE"
    echo ""
    echo "FAIL — $VIOLATIONS violation(s). Mutations must be inside Entry.Create().Execute() bodies."
    exit 1
fi
echo "PASS — 0 violations (no direct repo mutations in controllers)"
