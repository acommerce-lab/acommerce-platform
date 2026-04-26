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

# Rule 10: UI-only operations must not dispatch to the HTTP engine.
# SetLanguage / SetTheme / SignOut are pure client-side state mutations — they
# MUST be applied via Applier.ApplyLocalAsync (or UiPreferences.*).  Passing
# them to Engine.ExecuteAsync / Engine.DispatchAsync round-trips to the
# backend for no reason and breaks language/theme toggling when the API is
# down or unauthenticated.
while IFS=: read -r file line content; do
    [ -z "$file" ] && continue
    report "ui-op-routed-to-http" "$(realpath --relative-to="$ROOT" "$file")" "$line" "$content"
done < <(grep -HnE 'Engine\.(Execute|Dispatch)[A-Za-z]*\Async.*ClientOps\.(SetLanguage|SetTheme|SignOut|SetRtl)' $FILES 2>/dev/null || true)

# Rule 12: Any AuthController that queries users by PhoneNumber MUST call
# PhoneNormalization.Normalize first — otherwise "0501…", "+9665…" and
# "9665…" create three different records for the same person.
echo ""
echo "--- Rule 12: AuthController normalises PhoneNumber before query ---"
for ctrl in $(find "$ROOT/Apps" -name 'AuthController.cs' -not -path '*/bin/*' -not -path '*/obj/*'); do
    # Does the file query by PhoneNumber? If yes, it must reference PhoneNormalization.
    if grep -qE 'PhoneNumber\s*==' "$ctrl" && ! grep -qE 'PhoneNormalization\.Normalize' "$ctrl"; then
        rel=$(realpath --relative-to="$ROOT" "$ctrl")
        report "auth-missing-phone-normalization" "$rel" "0" \
            "AuthController queries by PhoneNumber but doesn't call PhoneNormalization.Normalize; users typing with/without +, spaces, or 05x prefix get treated as different people"
    fi
done

# Rule 13: A backend service that references another service's csproj for its
# entity model MUST restrict AddControllers() to its own assembly, otherwise
# the other controller also registers and produces an AmbiguousMatchException.
echo ""
echo "--- Rule 13: AddControllers must not scan piggy-backed service assemblies ---"
for prog in $(find "$ROOT/Apps" -name 'Program.cs' -path '*/Backend/*' -not -path '*/bin/*' -not -path '*/obj/*'); do
    csproj_dir=$(dirname "$prog")
    csproj=$(ls "$csproj_dir"/*.csproj 2>/dev/null | head -1)
    [ -z "$csproj" ] && continue
    # Does it reference another service's backend project (piggy-backing entities)?
    # Paths are relative, e.g. ..\..\..\Customer\Backend\Ashare.Api\Ashare.Api.csproj
    piggy=$(grep -E 'ProjectReference.*[/\\]Backend[/\\][^"]*\.Api\.csproj' "$csproj" 2>/dev/null | head -1 || true)
    [ -z "$piggy" ] && continue
    # It does; ensure AddControllers is restricted.
    if ! grep -qE 'PartManager\.ApplicationParts|\.AddControllers\(\)\s*\.ConfigureApplicationPartManager|AssemblyPart\(typeof\(Program\)\.Assembly\)' "$prog"; then
        rel=$(realpath --relative-to="$ROOT" "$prog")
        report "unrestricted-controller-scan" "$rel" "0" \
            "Program.cs piggy-backs another service's project but calls AddControllers() without restricting ApplicationParts — duplicate [Route] registrations will AmbiguousMatchException"
    fi
done

# Rule 11: Consumed RCL CSS cohesion.
# For each App frontend, cross-check that App.razor links the CSS of every
# libs/frontend/<Lib> that the .csproj references.  If a template widget is
# used via ProjectReference but its CSS is never linked, the widget renders
# unstyled at runtime (e.g. AcShell falling back to browser defaults).
echo ""
echo "--- Rule 11: RCL CSS cohesion (App.razor links every referenced frontend lib CSS) ---"
while IFS= read -r csproj; do
    [ -z "$csproj" ] && continue
    app_dir=$(dirname "$csproj")
    app_razor="$app_dir/Components/App.razor"
    [ -f "$app_razor" ] || continue
    # Library project references → package IDs expected in _content/<Pkg>/
    refs=$(grep -oE 'libs[/\\](frontend|kits)[/\\][^"]+\.csproj' "$csproj" 2>/dev/null | \
           tr '\\' '/' | sed 's|.*/||; s|\.csproj$||' | sort -u || true)
    # _content/<Pkg>/ links actually present in App.razor
    links=$(grep -oE '_content/[^/]+/' "$app_razor" 2>/dev/null | sed 's|_content/||; s|/$||' | sort -u || true)
    for pkg in $refs; do
        # Skip libs that contain NO css (only .razor components).
        # `|| true` guards against SIGPIPE tripping `set -eo pipefail`.
        # Search for the wwwroot under either old or new tree (any depth).
        has_css=$(find "$ROOT/libs" -type d -name "$pkg" 2>/dev/null \
                  -exec sh -c 'ls "$1"/wwwroot/*.css 2>/dev/null' _ {} \; \
                  | head -1 || true)
        [ -z "$has_css" ] && continue
        if ! echo "$links" | grep -qxF "$pkg" ; then
            report "missing-css-link [$pkg]" "$(realpath --relative-to="$ROOT" "$app_razor")" "1" "referenced via .csproj but App.razor doesn't link _content/$pkg/*.css"
        fi
    done
done < <(find "$ROOT/Apps" -name '*.csproj' -path '*/Frontend/*' -not -path '*/bin/*' -not -path '*/obj/*' 2>/dev/null | sort)

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
