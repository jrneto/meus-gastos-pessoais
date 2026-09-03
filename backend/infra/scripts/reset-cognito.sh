#!/bin/bash
# Remove todos os usuários do User Pool local no cognito-local, sem
# apagar o User Pool/App Client em si — zera os dados de teste sem
# precisar recriar o ambiente via local-init.sh. Lê o UserPoolId de
# backend/infra/.local-cognito-ids (gerado por init-cognito.sh).
#
# Uso: ./scripts/reset-cognito.sh (a partir de backend/infra/)
set -e

# Ver comentário equivalente em local-init.sh — evita que o Git Bash/MSYS
# (Windows) corrompa nomes/paths passados ao AWS CLI.
export MSYS_NO_PATHCONV=1

ENDPOINT="http://localhost:9229"
REGION="us-east-1"
STATE_FILE="$(dirname "$0")/../.local-cognito-ids"

aws_cognito() {
  aws --endpoint-url "$ENDPOINT" --region "$REGION" cognito-idp "$@"
}

if [ ! -f "$STATE_FILE" ]; then
  echo "$STATE_FILE não existe (ambiente ainda não inicializado), nada a zerar."
  exit 0
fi

USER_POOL_ID=$(grep USER_POOL_ID "$STATE_FILE" | cut -d= -f2 | tr -d '\r')

if [ -z "$USER_POOL_ID" ]; then
  echo "USER_POOL_ID não encontrado em $STATE_FILE, nada a zerar."
  exit 0
fi

# --query/--output text (mesmo padrão de init-cognito.sh). O AWS CLI
# pagina list-users automaticamente, cobrindo pools com mais usuários
# do que o limite de 60 por página.
# tr -d '\r' descarta o CR que o `--output text` do AWS CLI grava no
# Windows — ver comentário equivalente em reset-dynamodb.sh.
USERNAMES=$(aws_cognito list-users --user-pool-id "$USER_POOL_ID" \
  --query "Users[].Username" --output text | tr -d '\r')

if [ -z "$USERNAMES" ]; then
  echo "User Pool '$USER_POOL_ID' já está sem usuários."
  exit 0
fi

COUNT=0
for username in $USERNAMES; do
  aws_cognito admin-delete-user --user-pool-id "$USER_POOL_ID" \
    --username "$username" >/dev/null
  COUNT=$((COUNT + 1))
done

echo "Removidos $COUNT usuários do User Pool '$USER_POOL_ID'."
