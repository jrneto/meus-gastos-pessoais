#!/bin/sh
set -e

# O volume montado em /app/.cognito (persistência entre reinícios, ver
# docker-compose.yml) esconde o config.json copiado na imagem durante o
# build — copia de volta só se ainda não existir no volume.
if [ ! -f /app/.cognito/config.json ]; then
  cp /app/config.default.json /app/.cognito/config.json
fi

exec cognito-local
