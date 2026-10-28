# syntax=docker/dockerfile:1

# Nightly pg_dump of the external database (ConnectionStrings__Default) into /backups.
# pg_dump must be at least the server's major version: build with --build-arg POSTGRES_VERSION=18 if needed.
ARG POSTGRES_VERSION=17
FROM postgres:${POSTGRES_VERSION}-alpine

COPY --chmod=755 backup.sh entrypoint.sh /usr/local/bin/

ENV BACKUP_DIR=/backups \
    BACKUP_TIME_UTC=00:00 \
    BACKUP_ON_START=true \
    BACKUP_KEEP_DAILY=7 \
    BACKUP_KEEP_WEEKLY=4

VOLUME ["/backups"]

# Healthy while the newest dump is younger than 26 hours.
HEALTHCHECK --interval=30m --timeout=10s --start-period=10m \
    CMD find "$BACKUP_DIR/daily" -name '*.dump' -mmin -1560 | grep -q .

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
