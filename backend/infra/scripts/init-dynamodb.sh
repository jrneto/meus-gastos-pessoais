#!/bin/bash
# Cria (de forma idempotente) a tabela GastosApp-Local no LocalStack,
# com o mesmo modelo de dados (PK/SK, GSI1, GSI2) das tabelas reais de
# produção/homologação — ver backend/docs/architecture.md e
# backend/infra/terraform/environments/hom/dynamodb.tf (FEAT-18).
set -e

# Ver comentário equivalente em local-init.sh — evita que o Git Bash/MSYS
# (Windows) corrompa nomes/paths passados ao AWS CLI.
export MSYS_NO_PATHCONV=1

ENDPOINT="http://localhost:4566"
REGION="us-east-1"
TABLE_NAME="GastosApp-Local"

aws_ddb() {
  aws --endpoint-url "$ENDPOINT" --region "$REGION" dynamodb "$@"
}

if aws_ddb describe-table --table-name "$TABLE_NAME" >/dev/null 2>&1; then
  echo "Tabela '$TABLE_NAME' já existe, nada a fazer."
  exit 0
fi

echo "Criando tabela '$TABLE_NAME'..."

aws_ddb create-table \
  --table-name "$TABLE_NAME" \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
    AttributeName=GSI2PK,AttributeType=S \
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
    },
    {
      "IndexName": "GSI2",
      "KeySchema": [
        {"AttributeName":"GSI2PK","KeyType":"HASH"}
      ],
      "Projection": {"ProjectionType":"KEYS_ONLY"}
    }
  ]' >/dev/null

echo "Tabela '$TABLE_NAME' criada."
