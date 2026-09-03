#!/usr/bin/env bash
# Sobe o container local do Lambda trigger PostConfirmation do Cognito
# (FEAT-19) via Lambda Runtime Interface Emulator (RIE), pra chamar
# manualmente (ex.: Postman) sem depender do teste manual em
# tests/GastosApp.UnitTests/TesteLocal/AccountTriggerHandlerManualDebug.cs.
# Mesmo padrão de infra/lambda/local-env-up.sh (Api), mas mais simples:
# este trigger não valida JWT/JWKS, então não precisa compartilhar o
# namespace de rede do cognito-local — só a rede nomeada "gastosapp-local"
# (pra alcançar "gastosapp-localstack" pelo nome), com porta publicada
# normalmente.
#
# Diferente da Api (protocolo API Gateway, ver local-env-up.sh): este
# Lambda não usa Amazon.Lambda.AspNetCoreServer.Hosting, é invocado pelo
# Cognito com o evento puro como payload. O body do POST pro RIE é o
# próprio CognitoPostConfirmationEvent (ver
# src/GastosApp.CognitoTriggers/CognitoPostConfirmationEvent.cs), sem
# envelope de rota/headers HTTP.
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/local-env-up-account-trigger.sh
set -euo pipefail

# Git Bash/MSYS (Windows) reescreve argumentos começando com "/" como
# caminho de arquivo Windows antes de chegar no `docker` — mesmo achado
# de local-env-up.sh/backend/infra/CLAUDE.md.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")/../.."  # backend/

IMAGE_NAME="gastosapp-account-trigger-local-run"
CONTAINER_NAME="gastosapp-account-trigger-local-run-container"
RIE_DIR="infra/lambda/.rie"
RIE_PATH="$RIE_DIR/aws-lambda-rie"
RIE_URL="https://github.com/aws/aws-lambda-runtime-interface-emulator/releases/latest/download/aws-lambda-rie"
COGNITO_IDS_FILE="infra/.local-cognito-ids"

echo "==> Garantindo LocalStack + cognito-local no ar (FEAT-18)..."
docker compose -f infra/docker-compose.yml up -d

# Sempre idempotente (mesmo racional de local-env-up.sh) — garante que o
# SSM local tem os parâmetros mesmo se o compose acabou de recriar os
# containers.
echo "==> Garantindo Cognito/DynamoDB/Parameter Store locais inicializados..."
(cd infra && ./scripts/local-init.sh)

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
docker build -f infra/lambda/Dockerfile.build-account-trigger --target local-run -t "$IMAGE_NAME" .

echo "==> Subindo container em http://localhost:9001 ..."
docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true

# Só a rede nomeada (não --network container:gastosapp-cognito-local como
# a Api) — este handler nunca valida JWT, então não depende de resolver
# "localhost:9229". Cognito__ServiceURL aponta pro container por nome
# (alcançável por estar na mesma rede "gastosapp-local"), mesmo que o
# client Cognito nunca seja de fato chamado por este handler (AddInfrastructure
# registra o client incondicionalmente, ver AccountTriggerHandlerManualDebug.cs).
docker run -d \
  --name "$CONTAINER_NAME" \
  --network gastosapp-local \
  -p 9001:8080 \
  -v "$(pwd)/$RIE_PATH:/aws-lambda/aws-lambda-rie:ro" \
  --entrypoint /aws-lambda/aws-lambda-rie \
  -e DynamoDb__TableName=GastosApp-Local \
  -e DynamoDb__Region=us-east-1 \
  -e DynamoDb__ServiceURL=http://gastosapp-localstack:4566 \
  -e DynamoDb__AccessKey=test \
  -e DynamoDb__SecretKey=test \
  -e Cognito__Region=us-east-1 \
  -e Cognito__UserPoolId="$USER_POOL_ID" \
  -e Cognito__ClientId="$CLIENT_ID" \
  -e Cognito__ServiceURL=http://gastosapp-cognito-local:9229 \
  -e Cognito__AccessKey=test \
  -e Cognito__SecretKey=test \
  "$IMAGE_NAME" \
  /var/runtime/bootstrap

echo "==> Aguardando o container responder (warm-up do host Native AOT)..."
# Evento SEM "sub" em UserAttributes de propósito — cai no branch
# defensivo de AccountTriggerHandler.cs (só loga, não despacha
# EnsureAccountCommand), então o warm-up não grava lixo em
# GastosApp-Local.
health_event='{"version":"1","region":"us-east-1","userPoolId":"health-check","userName":"health-check","callerContext":{"awsSdkVersion":"health-check"},"triggerSource":"PostConfirmation_ConfirmSignUp","request":{"userAttributes":{}},"response":{}}'
ready=false
for i in $(seq 1 30); do
  if curl -sf -X POST "http://localhost:9001/2015-03-31/functions/function/invocations" -d "$health_event" >/dev/null; then
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

cat <<EOF

Ambiente local no ar:
  account-trigger (via RIE): POST http://localhost:9001/2015-03-31/functions/function/invocations

Chame com o body sendo o CognitoPostConfirmationEvent puro (sem envelope
de API Gateway, diferente da Api) — ver
src/GastosApp.CognitoTriggers/CognitoPostConfirmationEvent.cs pro shape
exato. Exemplo (troque "sub"/"email" pelos de um usuário já cadastrado
localmente — ver backend/infra/README.md, "Confirmando um usuário"):

{
  "version": "1",
  "region": "us-east-1",
  "userPoolId": "$USER_POOL_ID",
  "userName": "<sub-do-usuario>",
  "callerContext": { "awsSdkVersion": "test" },
  "triggerSource": "PostConfirmation_ConfirmSignUp",
  "request": { "userAttributes": { "sub": "<sub-do-usuario>", "email": "<email>" } },
  "response": {}
}

A resposta que volta é o mesmo evento (o Cognito real exige isso) — não
tem corpo específico de sucesso pra conferir; confirme o efeito colateral
consultando o item USER#<sub> em GastosApp-Local no LocalStack.

Quando terminar: ./infra/lambda/local-env-down-account-trigger.sh
EOF
