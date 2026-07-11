#!/bin/bash
set -e

echo "Iniciando setup do LocalStack..."

# Criar tabela DynamoDB GastosApp
awslocal dynamodb create-table \
  --table-name GastosApp \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --global-secondary-indexes '[
    {
      "IndexName": "GSI1",
      "KeySchema": [
        {"AttributeName":"GSI1PK","KeyType":"HASH"},
        {"AttributeName":"GSI1SK","KeyType":"RANGE"}
      ],
      "Projection": {"ProjectionType":"ALL"}
    }
  ]'

echo "Tabela GastosApp criada com sucesso."

# Criar User Pool do Cognito e extrair ID
USER_POOL_INFO=$(awslocal cognito-idp create-user-pool \
  --pool-name GastosAppUserPool \
  --auto-verified-attributes email \
  --policies '{
    "PasswordPolicy": {
      "MinimumLength": 8,
      "RequireUppercase": true,
      "RequireLowercase": true,
      "RequireNumbers": true,
      "RequireSymbols": false
    }
  }')

# Parse do UserPoolId suportando jq ou grep alternativo
if command -v jq &> /dev/null; then
  USER_POOL_ID=$(echo "$USER_POOL_INFO" | jq -r '.UserPool.Id')
else
  USER_POOL_ID=$(echo "$USER_POOL_INFO" | grep -o '"Id": "[^"]*' | head -n 1 | cut -d'"' -f4)
fi

echo "Cognito User Pool criado com ID: $USER_POOL_ID"

# Criar App Client para login de usuário e senha
CLIENT_INFO=$(awslocal cognito-idp create-user-pool-client \
  --user-pool-id "$USER_POOL_ID" \
  --client-name GastosAppClient \
  --explicit-auth-flows USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH ALLOW_CUSTOM_AUTH)

if command -v jq &> /dev/null; then
  CLIENT_ID=$(echo "$CLIENT_INFO" | jq -r '.UserPoolClient.ClientId')
else
  CLIENT_ID=$(echo "$CLIENT_INFO" | grep -o '"ClientId": "[^"]*' | head -n 1 | cut -d'"' -f4)
fi

echo "Cognito User Pool Client criado com ID: $CLIENT_ID"
echo "Setup do LocalStack concluído."
