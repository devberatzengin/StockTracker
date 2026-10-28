#!/bin/sh
# One backup: pg_dump (custom format) into $BACKUP_DIR/daily, a copy into weekly on Sundays, then pruning.
# Connection settings come from the Npgsql connection string ConnectionStrings__Default.
set -eu

: "${ConnectionStrings__Default:?ConnectionStrings__Default is not set}"

# Npgsql "Key=Value;..." -> libpq PG* variables. Values must not contain ';'.
old_ifs=$IFS
IFS=';'
for pair in $ConnectionStrings__Default; do
    key=$(printf '%s' "${pair%%=*}" | tr 'A-Z' 'a-z' | tr -d ' ')
    value=${pair#*=}
    case "$key" in
        host|server) export PGHOST="$value" ;;
        port) export PGPORT="$value" ;;
        database|db) export PGDATABASE="$value" ;;
        username|userid|user) export PGUSER="$value" ;;
        password|pwd) export PGPASSWORD="$value" ;;
        sslmode)
            case $(printf '%s' "$value" | tr 'A-Z' 'a-z') in
                verifyfull|verify-full) export PGSSLMODE=verify-full ;;
                verifyca|verify-ca) export PGSSLMODE=verify-ca ;;
                *) export PGSSLMODE=$(printf '%s' "$value" | tr 'A-Z' 'a-z') ;;
            esac ;;
    esac
done
IFS=$old_ifs

dir=${BACKUP_DIR:-/backups}
mkdir -p "$dir/daily" "$dir/weekly"
stamp=$(date -u +%Y-%m-%d-%H%M%S)
file="$dir/daily/stocktracker-$stamp.dump"

echo "$(date -u +%FT%TZ) backup started: $PGHOST/$PGDATABASE"
pg_dump --format=custom --no-owner --no-privileges --file="$file.partial"
mv "$file.partial" "$file"
echo "$(date -u +%FT%TZ) backup written: $file ($(du -h "$file" | cut -f1))"

# Sunday (UTC): keep a weekly copy.
if [ "$(date -u +%u)" = 7 ]; then
    cp "$file" "$dir/weekly/"
fi

prune() {
    ls -1t "$1"/*.dump 2>/dev/null | tail -n +"$(($2 + 1))" | while read -r old; do
        rm -f "$old"
        echo "pruned $old"
    done
}
prune "$dir/daily" "${BACKUP_KEEP_DAILY:-7}"
prune "$dir/weekly" "${BACKUP_KEEP_WEEKLY:-4}"
rm -f "$dir"/daily/*.partial
