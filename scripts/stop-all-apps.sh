#!/usr/bin/env bash
PIDFILE="/tmp/acommerce-pids.txt"
[ -f "$PIDFILE" ] || { echo "no pidfile"; exit 0; }
while read pid name port; do
    [ -z "$pid" ] && continue
    if kill -0 "$pid" 2>/dev/null; then kill "$pid" 2>/dev/null; fi
done < "$PIDFILE"
sleep 2
# Force-kill stragglers
while read pid name port; do
    [ -z "$pid" ] && continue
    if kill -0 "$pid" 2>/dev/null; then kill -9 "$pid" 2>/dev/null; fi
done < "$PIDFILE"
# Also kill any orphaned dotnet run processes
pkill -9 -f "dotnet run" 2>/dev/null || true
rm -f "$PIDFILE"
echo "stopped"
