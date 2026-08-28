#!/usr/bin/env bash
# Desliga o container da Api subido por local-env-up.sh. Não mexe em
# LocalStack/cognito-local (docker-compose.yml, FEAT-18) — pra derrubar
# esses também, use `docker compose -f infra/docker-compose.yml down`
# a partir de backend/infra/.
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/local-env-down.sh
set -euo pipefail

CONTAINER_NAME="gastosapp-api-local-run-container"

echo "Desligando $CONTAINER_NAME..."
docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || echo "(já estava desligado)"
