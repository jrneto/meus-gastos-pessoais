#!/bin/bash
# Remove todos os itens da tabela GastosApp-Local no LocalStack, sem
# apagar a tabela em si (estrutura/índices continuam intactos) — zera
# os dados de teste sem precisar recriar o ambiente via
# local-init.sh. Ver backend/infra/CLAUDE.md.
#
# Uso: ./scripts/reset-dynamodb.sh (a partir de backend/infra/)
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

if ! aws_ddb describe-table --table-name "$TABLE_NAME" >/dev/null 2>&1; then
  echo "Tabela '$TABLE_NAME' não existe, nada a zerar."
  exit 0
fi

# --query/--output text (mesmo padrão de init-cognito.sh) evita depender
# de jq, que não é pré-requisito deste projeto. O AWS CLI pagina o scan
# automaticamente, então isso já cobre tabelas com mais de 1MB de itens.
# tr -d '\r' descarta o CR que o `--output text` do AWS CLI grava no
# Windows — sem isso ele fica grudado no último campo de cada linha
# (SK) e quebra o JSON de --key mais abaixo.
ITEMS=$(aws_ddb scan --table-name "$TABLE_NAME" \
  --projection-expression "PK, SK" \
  --query "Items[].[PK.S, SK.S]" --output text | tr -d '\r')

if [ -z "$ITEMS" ]; then
  echo "Tabela '$TABLE_NAME' já está vazia."
  exit 0
fi

COUNT=0
while IFS=$'\t' read -r pk sk; do
  [ -z "$pk" ] && continue
  aws_ddb delete-item --table-name "$TABLE_NAME" \
    --key "{\"PK\":{\"S\":\"$pk\"},\"SK\":{\"S\":\"$sk\"}}" >/dev/null
  COUNT=$((COUNT + 1))
done <<< "$ITEMS"

echo "Removidos $COUNT itens de '$TABLE_NAME'."
