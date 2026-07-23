#!/bin/bash
# Gera backend/docs/openapi.json a partir do documento OpenAPI real da API.
#
# Sobe a API localmente (ambiente Development, conectando aos recursos
# reais da AWS — Cognito/Parameter Store, sem simulação, conforme
# backend/infra/CLAUDE.md), chama GET /openapi/v1.json, salva o
# resultado e encerra o processo.
#
# Rodar sempre que um endpoint mudar de contrato (novo campo, novo
# status code, novo endpoint) — ver backend/docs/constitution.md.
set -euo pipefail

cd "$(dirname "$0")/.."

PORT=5049
URL="http://localhost:${PORT}/openapi/v1.json"
OUT="docs/openapi.json"

echo "Subindo a API (Development) para exportar o contrato OpenAPI..."
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/GastosApp.Api --launch-profile http > /tmp/export-openapi.log 2>&1 &
PID=$!

cleanup() {
  kill "$PID" >/dev/null 2>&1 || true
  # No Windows/git-bash, `dotnet run` sobe um processo filho que o kill
  # acima nao alcanca — garante que nao fica API orfa rodando.
  if command -v taskkill >/dev/null 2>&1; then
    taskkill //F //IM dotnet.exe //T >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "Aguardando a API subir em $URL..."
for i in $(seq 1 30); do
  if curl -sf -o /dev/null "$URL"; then
    break
  fi
  sleep 1
done

STATUS=$(curl -s -o "$OUT" -w "%{http_code}" "$URL")
if [ "$STATUS" != "200" ]; then
  echo "Falha ao exportar o contrato: HTTP $STATUS. Log da API em /tmp/export-openapi.log" >&2
  exit 1
fi

echo "Contrato exportado para backend/$OUT"
