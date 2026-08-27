#!/usr/bin/env bash
# Builda a Api publicada em Native AOT dentro da mesma família de imagem
# base da Lambda real (provided.al2023), sobe num container acessível
# via Lambda Runtime Interface Emulator (RIE) e roda a suíte de testes
# integrados (FEAT-29) contra ela — sem nenhuma credencial/rede AWS
# real, reaproveitando LocalStack + cognito-local (FEAT-18) já
# existentes. É esse caminho — binário publicado, invocado como a
# Lambda real seria — que expõe erro específico de Native AOT antes de
# qualquer deploy real. Ver
# backend/specs/FEAT-29-testes-integrados/plan.md, "Container local".
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/run-local.sh
set -euo pipefail

cd "$(dirname "$0")/../.."  # backend/

IMAGE_NAME="gastosapp-api-local-run"
CONTAINER_NAME="gastosapp-api-local-run-container"
NETWORK_NAME="gastosapp-local"
RIE_DIR="infra/lambda/.rie"
RIE_PATH="$RIE_DIR/aws-lambda-rie"
# Sempre a última versão — o RIE é ferramenta de teste local, não um
# artefato de deploy (nunca embarcado na imagem publicada em
# Dockerfile.local-run), então não há necessidade de fixar uma versão.
RIE_URL="https://github.com/aws/aws-lambda-runtime-interface-emulator/releases/latest/download/aws-lambda-rie"
COGNITO_IDS_FILE="infra/.local-cognito-ids"

cleanup() {
  echo "Desligando $CONTAINER_NAME..."
  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "==> Garantindo LocalStack + cognito-local no ar (FEAT-18)..."
docker compose -f infra/docker-compose.yml up -d

if [ ! -f "$COGNITO_IDS_FILE" ]; then
  echo "==> Ambiente local ainda não inicializado — rodando local-init.sh..."
  (cd infra && ./scripts/local-init.sh)
fi

# shellcheck disable=SC1090
source "$COGNITO_IDS_FILE"

if [ -z "${USER_POOL_ID:-}" ] || [ -z "${CLIENT_ID:-}" ]; then
  echo "Erro: USER_POOL_ID/CLIENT_ID não encontrados em $COGNITO_IDS_FILE." >&2
  exit 1
fi

echo "==> Runtime Interface Emulator (RIE)..."
mkdir -p "$RIE_DIR"
if [ ! -f "$RIE_PATH" ]; then
  echo "Baixando $RIE_URL..."
  curl -sSL -o "$RIE_PATH" "$RIE_URL"
  chmod +x "$RIE_PATH"
fi

echo "==> Construindo imagem Native AOT ($IMAGE_NAME)..."
docker build -f infra/lambda/Dockerfile.local-run --target local-run -t "$IMAGE_NAME" .

echo "==> Subindo container em http://localhost:9000 ..."
docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true

# ASPNETCORE_ENVIRONMENT=Testing pula o AddAwsParameterStore de
# Program.cs (mesmo mecanismo já usado por GastosApp.ComponentTests) —
# necessário porque os parâmetros Cognito:ServiceURL/AccessKey/SecretKey
# já seedados no SSM local (init-parameter-store.sh) apontam pra
# "localhost:9229", correto só quando quem consome é um processo rodando
# no host (dotnet run); de dentro deste container, o endereço certo é o
# nome do container na rede Docker ("gastosapp-cognito-local"). Pulando
# o Parameter Store, todo config necessário é passado direto como
# variável de ambiente abaixo, sempre com o hostname de rede correto.
docker run -d --rm \
  --name "$CONTAINER_NAME" \
  --network "$NETWORK_NAME" \
  -p 9000:8080 \
  -v "$(pwd)/$RIE_PATH:/aws-lambda/aws-lambda-rie:ro" \
  --entrypoint /aws-lambda/aws-lambda-rie \
  -e ASPNETCORE_ENVIRONMENT=Testing \
  -e Cognito__Region=us-east-1 \
  -e Cognito__UserPoolId="$USER_POOL_ID" \
  -e Cognito__ClientId="$CLIENT_ID" \
  -e Cognito__ServiceURL=http://gastosapp-cognito-local:9229 \
  -e Cognito__AccessKey=test \
  -e Cognito__SecretKey=test \
  -e DynamoDb__TableName=GastosApp-Local \
  -e DynamoDb__Region=us-east-1 \
  -e DynamoDb__ServiceURL=http://gastosapp-localstack:4566 \
  -e DynamoDb__AccessKey=test \
  -e DynamoDb__SecretKey=test \
  "$IMAGE_NAME" \
  /var/runtime/bootstrap

echo "==> Aguardando o container responder (warm-up do host Native AOT)..."
health_event='{"version":"2.0","routeKey":"$default","rawPath":"/health","rawQueryString":"","headers":{},"requestContext":{"http":{"method":"GET","path":"/health"}},"isBase64Encoded":false}'
ready=false
for i in $(seq 1 30); do
  if curl -sf -X POST "http://localhost:9000/2015-03-31/functions/function/invocations" -d "$health_event" >/dev/null; then
    echo "Container pronto."
    ready=true
    break
  fi
  sleep 1
done

if [ "$ready" != "true" ]; then
  echo "Erro: container não respondeu depois de 30 tentativas — ver 'docker logs $CONTAINER_NAME'." >&2
  docker logs "$CONTAINER_NAME" || true
  exit 1
fi

echo "==> Rodando testes integrados (modo local)..."
INTEGRATION_TESTS_MODE=local dotnet test tests/GastosApp.IntegrationTests -c Release --filter "Category=Integration"
