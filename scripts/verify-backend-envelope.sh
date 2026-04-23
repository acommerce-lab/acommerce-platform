#!/usr/bin/env bash
# verify-backend-envelope.sh
# Ensures every controller endpoint returns OperationEnvelope (OkEnvelope / error helpers),
# never a raw return Ok(data).
# Exit 1 if violations found.

set -eo pipefail

ROOT="${ROOT:-$(pwd)}"
TARGET="${1:-$ROOT}"
VIOLATIONS=0
VIOLATIONS_FILE=$(mktemp)
trap "rm -f $VIOLATIONS_FILE" EXIT

report() {
    echo "  ✗ [envelope] $1:$2" >> "$VIOLATIONS_FILE"
    echo "      $3" >> "$VIOLATIONS_FILE"
    VIOLATIONS=$((VIOLATIONS + 1))
}

# Scan all controllers under target
while IFS= read -r file; do
    lineno=0
    while IFS= read -r line; do
        lineno=$((lineno + 1))
        # Detect raw return Ok(...) — must use OkEnvelope / envelope helpers
        if echo "$line" | grep -qE 'return Ok\((?!env\b)(?!envelope\b)' 2>/dev/null; then
            report "$file" "$lineno" "$line"
        fi
        # Detect return Json(...) without envelope
        if echo "$line" | grep -qE 'return Json\(' 2>/dev/null; then
            report "$file" "$lineno" "$line"
        fi
    done < "$file"
done < <(find "$TARGET" -path '*/Controllers/*.cs' -not -path '*/obj/*' -not -path '*/bin/*')

echo "=== verify-backend-envelope ==="
if [ "$VIOLATIONS" -gt 0 ]; then
    cat "$VIOLATIONS_FILE"
    echo ""
    echo "FAIL — $VIOLATIONS violation(s). All endpoints must return OperationEnvelope via OkEnvelope/error helpers."
    exit 1
fi
echo "PASS — 0 violations (all endpoints use envelope helpers)"
