#!/usr/bin/env bash
# Usage: ./scripts/verify-runtime.sh
#
# Wraps verify-runtime.mjs with setup checks and friendly output.
# Assumes the frontend apps are already running (start them separately).
#
# Typical workflow:
#   1. Start Order.Web, Vendor.Web, Ashare.Web (from Visual Studio or CLI)
#   2. ./scripts/verify-runtime.sh

set -eo pipefail
ROOT="${ROOT:-$(pwd)}"
SCRIPTS_DIR="$ROOT/scripts"

cd "$SCRIPTS_DIR"

# Check Node + Playwright
if ! command -v node &>/dev/null; then
    echo "✗ Node.js not installed. Install from https://nodejs.org"
    exit 1
fi

if [ ! -d "$SCRIPTS_DIR/node_modules/playwright" ]; then
    echo "ℹ Installing Playwright (one-time setup)..."
    npm install --silent
    npx playwright install chromium --with-deps 2>&1 | tail -5
fi

# Check if any catalog is reachable
REACHABLE=0
for url in "http://localhost:5801/catalog" "http://localhost:5600/catalog"; do
    if curl -s -o /dev/null -w "%{http_code}" "$url" 2>/dev/null | grep -q "200\|3.."; then
        REACHABLE=1
        break
    fi
done

if [ "$REACHABLE" = "0" ]; then
    echo "⚠ No catalog pages reachable. Make sure at least one of these is running:"
    echo "    Order.Web:  http://localhost:5801/catalog"
    echo "    Vendor.Web: http://localhost:5802/catalog"
    echo "    Ashare.Web: http://localhost:5600/catalog"
    echo ""
    echo "Override URLs with: CATALOG_URLS=http://... ./scripts/verify-runtime.sh"
    exit 1
fi

node "$SCRIPTS_DIR/verify-runtime.mjs"
