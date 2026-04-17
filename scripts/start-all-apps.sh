#!/usr/bin/env bash
# Starts every backend + frontend in the background, redirecting logs.
# Each app's stdout/stderr goes to /tmp/<name>.log
# PIDs are written to /tmp/acommerce-pids.txt for later cleanup.

set -eo pipefail
ROOT="${ROOT:-$(pwd)}"
PIDFILE="/tmp/acommerce-pids.txt"
: > "$PIDFILE"

start() {
    local name="$1"; local path="$2"; local port="$3"
    local log="/tmp/$name.log"
    : > "$log"
    # PlatformDataRoot in Program.cs walks up to the repo's .sln and uses
    # <repo>/data automatically — works from VS F5 too, no env var required.
    (cd "$ROOT/$path" && \
        ASPNETCORE_ENVIRONMENT=Development \
        exec dotnet run --no-build -c Debug >> "$log" 2>&1) &
    local pid=$!
    echo "$pid $name $port" >> "$PIDFILE"
    echo "started $name (pid=$pid, port=$port)"
}

# Wait for a backend to finish seeding before the next one in the same
# platform opens a SQLite connection — EF Core's prepared-statement cache
# poisons if the user table doesn't exist at connection-prepare time.
wait_ready() {
    local port="$1"; local log="$2"; local marker="${3:-Seeding complete}"
    for _ in $(seq 1 60); do
        if grep -qE "$marker" "$log" 2>/dev/null; then return 0; fi
        sleep 0.5
    done
}

# Customer-facing backends seed first; admin backends follow so they open
# their first connection against a fully-populated schema.
start Order.Api            Apps/Order/Customer/Backend/Order.Api            5101
wait_ready 5101 /tmp/Order.Api.log
start Vendor.Api           Apps/Order/Vendor/Backend/Vendor.Api             5201
start Order.Admin.Api      Apps/Order/Admin/Backend/Order.Admin.Api         5102

start Ashare.Api           Apps/Ashare/Customer/Backend/Ashare.Api          5500
wait_ready 5500 /tmp/Ashare.Api.log
start Ashare.Admin.Api     Apps/Ashare/Admin/Backend/Ashare.Admin.Api       5502

# Frontends
start Order.Web            Apps/Order/Customer/Frontend/Order.Web           5701
start Order.Admin.Web      Apps/Order/Admin/Frontend/Order.Admin.Web        5702
start Vendor.Web           Apps/Order/Vendor/Frontend/Vendor.Web            5801
start Ashare.Web           Apps/Ashare/Customer/Frontend/Ashare.Web         5600
start Ashare.Provider.Web  Apps/Ashare/Provider/Frontend/Ashare.Provider.Web 5601
start Ashare.Admin.Web     Apps/Ashare/Admin/Frontend/Ashare.Admin.Web      5602

echo ""
echo "Waiting for ports to open..."
for port in 5101 5102 5201 5500 5502 5701 5702 5801 5600 5601 5602; do
    for _ in $(seq 1 30); do
        if curl -sS -o /dev/null --connect-timeout 1 "http://localhost:$port/" 2>/dev/null; then
            echo "  ✓ $port up"
            break
        fi
        sleep 0.5
    done
done
