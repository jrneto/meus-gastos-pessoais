#!/usr/bin/env bash
# Sobe o container local do Lambda trigger CustomMessage do Cognito
# (FEAT-34) via Lambda Runtime Interface Emulator (RIE), pra chamar
# manualmente (ex.: Postman). Mesmo padrão de
# local-env-up-account-trigger.sh, mas ainda mais simples: este handler
# (CustomMessageTriggerHandler.cs) não depende de DynamoDB nem Cognito —
# só formata texto a partir do próprio evento recebido — então nenhuma
# variável de ambiente de config é necessária, e a rede Docker default já
# basta (sem precisar de "gastosapp-local").
#
# Diferente da Api (protocolo API Gateway): o body do POST pro RIE é o
# próprio CognitoCustomMessageEvent puro (ver
# src/GastosApp.CognitoTriggers.CustomMessage/CognitoCustomMessageEvent.cs),
# sem envelope de rota/headers HTTP.
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/local-env-up-custom-message-trigger.sh
set -euo pipefail

export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")/../.."  # backend/

IMAGE_NAME="gastosapp-custom-message-trigger-local-run"
CONTAINER_NAME="gastosapp-custom-message-trigger-local-run-container"
RIE_DIR="infra/lambda/.rie"
RIE_PATH="$RIE_DIR/aws-lambda-rie"
RIE_URL="https://github.com/aws/aws-lambda-runtime-interface-emulator/releases/latest/download/aws-lambda-rie"

echo "==> Runtime Interface Emulator (RIE)..."
mkdir -p "$RIE_DIR"
if [ ! -f "$RIE_PATH" ]; then
  echo "Baixando $RIE_URL..."
  curl -sSL -o "$RIE_PATH" "$RIE_URL"
  chmod +x "$RIE_PATH"
fi

echo "==> Construindo imagem Native AOT ($IMAGE_NAME)..."
docker build -f infra/lambda/Dockerfile.build-custom-message-trigger --target local-run -t "$IMAGE_NAME" .

echo "==> Subindo container em http://localhost:9002 ..."
docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true

docker run -d \
  --name "$CONTAINER_NAME" \
  -p 9002:8080 \
  -v "$(pwd)/$RIE_PATH:/aws-lambda/aws-lambda-rie:ro" \
  --entrypoint /aws-lambda/aws-lambda-rie \
  "$IMAGE_NAME" \
  /var/runtime/bootstrap

echo "==> Aguardando o container responder (warm-up do host Native AOT)..."
# TriggerSource fora do escopo tratado (nem CustomMessage_SignUp/
# ResendCode/ForgotPassword) — handler devolve o evento sem alterar
# Response, sem nenhum efeito colateral (não há DynamoDB/Cognito aqui).
health_event='{"version":"1","region":"us-east-1","userPoolId":"health-check","userName":"health-check","callerContext":{"awsSdkVersion":"health-check"},"triggerSource":"health-check","request":{"userAttributes":{},"codeParameter":"{####}"},"response":{}}'
ready=false
for i in $(seq 1 30); do
  if curl -sf -X POST "http://localhost:9002/2015-03-31/functions/function/invocations" -d "$health_event" >/dev/null; then
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
  custom-message-trigger (via RIE): POST http://localhost:9002/2015-03-31/functions/function/invocations

Chame com o body sendo o CognitoCustomMessageEvent puro (sem envelope de
API Gateway) — ver
src/GastosApp.CognitoTriggers.CustomMessage/CognitoCustomMessageEvent.cs
pro shape exato. Exemplo (triggerSource precisa ser um dos 3 tratados —
ver CustomMessageTriggerHandler.cs — pra "response.emailMessage"/
"emailSubject" virem preenchidos na resposta):

{
  "version": "1",
  "region": "us-east-1",
  "userPoolId": "<qualquer valor>",
  "userName": "<sub-ou-email>",
  "callerContext": { "awsSdkVersion": "test" },
  "triggerSource": "CustomMessage_SignUp",
  "request": {
    "userAttributes": { "name": "Fulano", "email": "fulano@jrnexpenses.com" },
    "codeParameter": "{####}"
  },
  "response": {}
}

A resposta que volta é o mesmo evento, com "response.emailMessage"/
"emailSubject" preenchidos — dá pra conferir o template renderizado
direto na resposta do Postman, sem precisar de e-mail real nem SES.

Quando terminar: ./infra/lambda/local-env-down-custom-message-trigger.sh
EOF
