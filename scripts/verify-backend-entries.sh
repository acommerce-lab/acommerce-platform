#!/usr/bin/env bash
# verify-backend-entries.sh
# Verifies that every Entry.Create call (in code, not comments) has the minimum required shape:
#   .From(...) .To(...) .Execute(...) .Build()
# Exit 1 if violations found.

set -eo pipefail

ROOT="${ROOT:-$(pwd)}"
TARGET="${1:-$ROOT}"
VIOLATIONS=0
VIOLATIONS_FILE=$(mktemp)
trap "rm -f $VIOLATIONS_FILE" EXIT

report() {
    echo "  ✗ [entries] $1:$2" >> "$VIOLATIONS_FILE"
    echo "      $3" >> "$VIOLATIONS_FILE"
    VIOLATIONS=$((VIOLATIONS + 1))
}

while IFS= read -r file; do
    # Strip comment lines (// ... and /// ...) before counting
    stripped=$(grep -v '^\s*//' "$file" 2>/dev/null || true)

    entry_count=$(echo "$stripped" | grep -c 'Entry\.Create(' || true)
    [ "$entry_count" -eq 0 ] && continue

    from_count=$(echo "$stripped"  | grep -c '\.From(' || true)
    to_count=$(echo "$stripped"    | grep -c '\.To(' || true)
    exec_count=$(echo "$stripped"  | grep -c '\.Execute(' || true)
    build_count=$(echo "$stripped" | grep -c '\.Build()' || true)

    if [ "$from_count" -lt "$entry_count" ]; then
        report "$file" "?" "Entry.Create missing .From() — found $from_count of $entry_count"
    fi
    if [ "$to_count" -lt "$entry_count" ]; then
        report "$file" "?" "Entry.Create missing .To() — found $to_count of $entry_count"
    fi
    if [ "$exec_count" -lt "$entry_count" ]; then
        report "$file" "?" "Entry.Create missing .Execute() — found $exec_count of $entry_count"
    fi
    if [ "$build_count" -lt "$entry_count" ]; then
        report "$file" "?" "Entry.Create missing .Build() — found $build_count of $entry_count"
    fi

    raw_exec=$(echo "$stripped" | grep -c '_engine\.Execute(' || true)
    if [ "$raw_exec" -gt 0 ]; then
        report "$file" "?" "Use ExecuteEnvelopeAsync, not _engine.Execute() directly"
    fi
done < <(find "$TARGET" -path '*/Controllers/*.cs' -not -path '*/obj/*' -not -path '*/bin/*')

echo "=== verify-backend-entries ==="
if [ "$VIOLATIONS" -gt 0 ]; then
    cat "$VIOLATIONS_FILE"
    echo ""
    echo "FAIL — $VIOLATIONS violation(s). Every Entry.Create must have From/To/Execute/Build."
    exit 1
fi
echo "PASS — 0 violations (all entry shapes valid)"
