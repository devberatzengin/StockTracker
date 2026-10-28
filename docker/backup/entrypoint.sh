#!/bin/sh
# Runs backup.sh every day at $BACKUP_TIME_UTC (default 00:00 UTC = 03:00 Istanbul).
# "backup-now" as the command runs one backup and exits: docker compose run --rm backup backup-now
set -eu

if [ "${1:-}" = "backup-now" ]; then
    exec /usr/local/bin/backup.sh
fi

run() {
    /usr/local/bin/backup.sh || echo "$(date -u +%FT%TZ) backup FAILED" >&2
}

if [ "${BACKUP_ON_START:-true}" = "true" ]; then
    run
fi

hour=${BACKUP_TIME_UTC%%:*}
minute=${BACKUP_TIME_UTC##*:}
while true; do
    now=$(date -u +%s)
    next=$((now - now % 86400 + ${hour#0} * 3600 + ${minute#0} * 60))
    if [ "$next" -le "$now" ]; then
        next=$((next + 86400))
    fi
    echo "next backup at $(date -u -d "@$next" +%FT%TZ)"
    sleep $((next - now))
    run
done
