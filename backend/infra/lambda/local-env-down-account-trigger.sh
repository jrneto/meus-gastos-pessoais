#!/usr/bin/env bash
# Desliga o container do account-trigger subido por
# local-env-up-account-trigger.sh. Não mexe em LocalStack/cognito-local
# (docker-compose.yml, FEAT-18) nem no container da Api (se estiver no
# ar via local-env-up.sh) — pra derrubar LocalStack/cognito-local também,
# use `docker compose -f infra/docker-compose.yml down` a partir de
# backend/infra/.
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/local-env-down-account-trigger.sh
set -euo pipefail

CONTAINER_NAME="gastosapp-account-trigger-local-run-container"

echo "Desligando $CONTAINER_NAME..."
docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || echo "(já estava desligado)"
