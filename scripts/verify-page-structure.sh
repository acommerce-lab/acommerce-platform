#!/usr/bin/env bash
# Usage: ./scripts/verify-page-structure.sh
# Enforces structural rules on all .razor files.
# Exits 1 if any violations found.

set -eo pipefail

ROOT="${ROOT:-$(pwd)}"
VIOLATIONS=0
VIOLATIONS_FILE=$(mktemp)
trap "rm -f $VIOLATIONS_FILE" EXIT

report() {
    local rule="$1"
    local file="$2"
    local line="$3"
    local content="$4"
    echo "  ✗ [$rule] $file:$line" >> "$VIOLATIONS_FILE"
    echo "      $content" >> "$VIOLATIONS_FILE"
    VIOLATIONS=$((VIOLATIONS + 1))
}

# Collect only .razor files under Apps/ (templates and widgets may legitimately
# use raw HTML — rules apply to page consumers only)
FILES=$(find "$ROOT/Apps" -name '*.razor' -not -path '*/bin/*' -not -path '*/obj/*' \
  -not -name 'App.razor' \
  -not -name 'Routes.razor' \
  -not -name 'MainLayout.razor' \
  -not -name '_Imports.razor')

echo "=== Layer 1: Page Structure Verification ==="
echo "Scanning $(echo "$FILES" | wc -l) page files..."
echo ""

# Rule 1: No raw <button> tags (use AcButton)
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    # Allow <button> only in specific cases: inside chat inputs that need native behavior
    # For now, reject all
    report "no-raw-button" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
done < <(grep -HnE '<button\s+[^>]*class="(btn|btn-)' $FILES 2>/dev/null || true)

# Rule 2: No class="btn ..." (Bootstrap button class) on <a>
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    report "no-bootstrap-btn-link" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
done < <(grep -HnE '<a\s+[^>]*class="[^"]*\bbtn\b' $FILES 2>/dev/null || true)

# Rule 3: No Bootstrap grid (col-md-* / col-sm-* / standalone "row")
# Allow our own prefixed classes: ac-row, acc-row, acm-row, etc.
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    report "no-bootstrap-grid" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
done < <(grep -HnE 'class="[^"]*\b(col-md-|col-sm-|col-lg-|col-xs-)[0-9]' $FILES 2>/dev/null || true)

# Rule 3b: No standalone "row" class (Bootstrap) — but allow *-row suffixes
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    # Must match " row " or "row " or class="row" exactly, not acc-row, ac-row etc
    if echo "$content" | grep -qE 'class="[^"]*(^|\s)row(\s|")' ; then
        report "no-bootstrap-row" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
    fi
done < <(grep -HnE 'class="row|class="[^"]* row ' $FILES 2>/dev/null || true)

# Rule 4: No raw table/thead/tbody (use AcCard list instead)
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    report "no-table-layout" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
done < <(grep -HnE '<table\s+class="(table|table-)' $FILES 2>/dev/null || true)

# Rule 5: No spinner-border (use AcSpinner or AcLoadingState)
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    report "no-bootstrap-spinner" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
done < <(grep -HnE 'class="[^"]*\bspinner-border\b' $FILES 2>/dev/null || true)

# Rule 6: No raw <div class="alert alert-*"> (use AcAlert)
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    report "no-bootstrap-alert" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
done < <(grep -HnE 'class="[^"]*\balert\s+alert-' $FILES 2>/dev/null || true)

# Rule 7: No raw <div class="card"> (use AcCard)
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    # Skip s-card / ac-card (our own)
    report "no-bootstrap-card" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
done < <(grep -HnE 'class="(card|card-body|card-header)\b' $FILES 2>/dev/null || true)

# Rule 8: No inline color values (must use CSS vars)
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    # Match style="color: #hexvalue" or style="background: rgb(...)" etc
    # Skip if uses var(--*)
    if echo "$content" | grep -qE 'style="[^"]*color:\s*#[0-9a-fA-F]+|style="[^"]*background:\s*(#|rgb)' ; then
        if ! echo "$content" | grep -q 'var(--ac-'; then
            report "no-inline-color" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
        fi
    fi
done < <(grep -HnE 'style="[^"]*(color|background):\s*[^"]*"' $FILES 2>/dev/null || true)

# Rule 9: No form-control (use AcInput or ac-input)
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    report "no-form-control" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
done < <(grep -HnE 'class="[^"]*\bform-control\b(?![-])' $FILES 2>/dev/null || true)

echo ""
echo "=== Report ==="
if [ "$VIOLATIONS" -gt 0 ]; then
    echo "Violations found: $VIOLATIONS"
    echo ""
    cat "$VIOLATIONS_FILE"
    echo ""
    echo "See docs/DESIGN-QA-METHODOLOGY.md for the Guaranteed-Design Template."
    exit 1
fi

echo "✅ All pages follow structural rules."
exit 0
