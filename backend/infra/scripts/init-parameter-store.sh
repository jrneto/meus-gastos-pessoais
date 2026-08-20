#!/bin/bash
# Popula (de forma idempotente) o prefixo /GastosApp/ no SSM Parameter
# Store do LocalStack, com os mesmos parâmetros de Cognito que hoje
# existem em produção/homologação (ver
# backend/infra/terraform/environments/hom/parameter-store.tf), mais
# ServiceURL/AccessKey/SecretKey — só usados quando presentes, sinalizam
# "modo local" pro backend (ver AddCognitoSdk/AddCognitoAuth, FEAT-18).
set -e

# Ver comentário equivalente em local-init.sh — evita que o Git Bash/MSYS
# (Windows) corrompa nomes/paths passados ao AWS CLI.
export MSYS_NO_PATHCONV=1

ENDPOINT="http://localhost:4566"
REGION="us-east-1"
STATE_FILE="$(dirname "$0")/../.local-cognito-ids"

if [ ! -f "$STATE_FILE" ]; then
  echo "Erro: $STATE_FILE não encontrado. Rode init-cognito.sh antes." >&2
  exit 1
fi

# shellcheck disable=SC1090
source "$STATE_FILE"

put_param() {
  aws --endpoint-url "$ENDPOINT" --region "$REGION" ssm put-parameter \
    --name "$1" --value "$2" --type String --overwrite >/dev/null
}

echo "Escrevendo parâmetros em /GastosApp/..."

put_param "/GastosApp/Cognito/UserPoolId" "$USER_POOL_ID"
put_param "/GastosApp/Cognito/ClientId" "$CLIENT_ID"
put_param "/GastosApp/Cognito/Region" "$REGION"
put_param "/GastosApp/Cognito/ServiceURL" "http://localhost:9229"
put_param "/GastosApp/Cognito/AccessKey" "test"
put_param "/GastosApp/Cognito/SecretKey" "test"

echo "Parâmetros escritos com sucesso."