#!/usr/bin/env bash
# Restores a backup into a throwaway PostgreSQL container and checks it.
#
#   scripts/backup/restore-test.sh path/to/stocktracker-2026-10-28-000000.dump
#   scripts/backup/restore-test.sh --volume stocktracker_backups   # newest daily dump in that volume
#
# Checks: pg_restore succeeds, migrations and core tables are present, and every stock balance equals
# the sum of its ledger movements. Exits non-zero on any failure. Needs Docker only.
set -euo pipefail

image="postgres:${POSTGRES_VERSION:-17}-alpine"
container="st-restore-test-$$"

usage() { sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }
[[ $# -ge 1 ]] || usage

mount=()
if [[ $1 == --volume ]]; then
    [[ $# -eq 2 ]] || usage
    mount=(-v "$2:/backups:ro")
else
    [[ -f $1 ]] || { echo "no such file: $1" >&2; exit 2; }
fi

cleanup() { docker rm -f "$container" >/dev/null 2>&1 || true; }
trap cleanup EXIT

docker run -d --name "$container" -e POSTGRES_PASSWORD=restore-test -e POSTGRES_DB=restored "${mount[@]}" "$image" >/dev/null
for _ in $(seq 1 60); do
    docker exec "$container" pg_isready -U postgres -d restored >/dev/null 2>&1 && break
    sleep 1
done
# The entrypoint restarts the server once after init; wait for the final one.
sleep 2
for _ in $(seq 1 30); do
    docker exec "$container" psql -U postgres -d restored -c 'select 1' >/dev/null 2>&1 && break
    sleep 1
done

if [[ ${#mount[@]} -gt 0 ]]; then
    dump=$(docker exec "$container" sh -c 'ls -1t /backups/daily/*.dump 2>/dev/null | head -n 1')
    [[ -n $dump ]] || { echo "no dump in volume $2" >&2; exit 1; }
else
    docker cp "$1" "$container:/tmp/restore.dump" >/dev/null
    dump=/tmp/restore.dump
fi
echo "restoring $dump"

docker exec "$container" pg_restore -U postgres -d restored --no-owner --no-privileges --exit-on-error "$dump"

sql() { docker exec "$container" psql -U postgres -d restored -tAX -c "$1"; }

migrations=$(sql 'select count(*) from "__EFMigrationsHistory"')
echo "migrations:      $migrations"
for table in users products items locations stock_balances stock_movements assignments count_sessions audit_logs; do
    printf '%-16s %s\n' "$table:" "$(sql "select count(*) from $table")"
done

mismatches=$(sql "
    select count(*) from (
        select b.product_id, b.location_id, b.quantity,
               coalesce((select sum(m.quantity) from stock_movements m
                         where m.item_id is null and m.product_id = b.product_id and m.to_location_id = b.location_id), 0)
             - coalesce((select sum(m.quantity) from stock_movements m
                         where m.item_id is null and m.product_id = b.product_id and m.from_location_id = b.location_id), 0) as ledger
        from stock_balances b
    ) x where quantity <> ledger")
echo "balance ≠ ledger: $mismatches"

if [[ $migrations -eq 0 || $mismatches -ne 0 ]]; then
    echo "RESTORE TEST FAILED" >&2
    exit 1
fi
echo "restore test passed"
