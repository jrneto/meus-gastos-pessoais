#!/usr/bin/env bash
# Desliga o container do custom-message-trigger subido por
# local-env-up-custom-message-trigger.sh. Não mexe em nenhum outro
# container (este trigger não depende de LocalStack/cognito-local).
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/local-env-down-custom-message-trigger.sh
set -euo pipefail

CONTAINER_NAME="gastosapp-custom-message-trigger-local-run-container"

echo "Desligando $CONTAINER_NAME..."
docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || echo "(já estava desligado)"
