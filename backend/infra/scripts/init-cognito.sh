#!/bin/bash
# Cria (de forma idempotente) o User Pool + App Client no cognito-local,
# usados pelo backend em desenvolvimento local (FEAT-18). Escreve os IDs
# resultantes em backend/infra/.local-cognito-ids, consumido por
# init-parameter-store.sh.
set -e

# Ver comentário equivalente em local-init.sh — evita que o Git Bash/MSYS
# (Windows) corrompa nomes/paths passados ao AWS CLI.
export MSYS_NO_PATHCONV=1

ENDPOINT="http://localhost:9229"
REGION="us-east-1"
POOL_NAME="GastosAppUserPool"
CLIENT_NAME="GastosAppClient"
STATE_FILE="$(dirname "$0")/../.local-cognito-ids"

aws_cognito() {
  aws --endpoint-url "$ENDPOINT" --region "$REGION" cognito-idp "$@"
}

echo "Verificando User Pool '$POOL_NAME' no cognito-local..."

USER_POOL_ID=$(aws_cognito list-user-pools --max-results 20 \
  --query "UserPools[?Name=='$POOL_NAME'].Id | [0]" --output text)

if [ -z "$USER_POOL_ID" ] || [ "$USER_POOL_ID" = "None" ]; then
  echo "Criando User Pool '$POOL_NAME'..."
  USER_POOL_ID=$(aws_cognito create-user-pool \
    --pool-name "$POOL_NAME" \
    --username-attributes email \
    --auto-verified-attributes email \
    --policies '{
      "PasswordPolicy": {
        "MinimumLength": 8,
        "RequireUppercase": true,
        "RequireLowercase": true,
        "RequireNumbers": true,
        "RequireSymbols": true,
        "TemporaryPasswordValidityDays": 7
      }
    }' \
    --query "UserPool.Id" --output text)
fi

echo "User Pool: $USER_POOL_ID"

CLIENT_ID=$(aws_cognito list-user-pool-clients --user-pool-id "$USER_POOL_ID" --max-results 20 \
  --query "UserPoolClients[?ClientName=='$CLIENT_NAME'].ClientId | [0]" --output text)

if [ -z "$CLIENT_ID" ] || [ "$CLIENT_ID" = "None" ]; then
  echo "Criando App Client '$CLIENT_NAME'..."
  CLIENT_ID=$(aws_cognito create-user-pool-client \
    --user-pool-id "$USER_POOL_ID" \
    --client-name "$CLIENT_NAME" \
    --explicit-auth-flows ALLOW_USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH ALLOW_USER_SRP_AUTH \
    --query "UserPoolClient.ClientId" --output text)
fi

echo "App Client: $CLIENT_ID"

cat > "$STATE_FILE" <<EOF
USER_POOL_ID=$USER_POOL_ID
CLIENT_ID=$CLIENT_ID
EOF

echo "IDs salvos em $STATE_FILE"
