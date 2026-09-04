#!/bin/bash
# Recria a tabela DynamoDB de homologação (GastosApp-Hom) do zero, via
# Terraform — mais barato e instantâneo que zerar item a item (scan +
# delete-item, ver backend/infra/scripts/reset-dynamodb.sh, que só
# existe para o LocalStack local). Tabela é PAY_PER_REQUEST, sem PITR/
# backup contínuo/deletion_protection (dynamodb.tf), então destruir e
# recriar não tem custo nem exige mais nenhum passo de limpeza.
#
# Usa `terraform apply -replace` (não `destroy -target` + `apply`):
# -target em destroy arrasta em cascata qualquer recurso que referencie
# a tabela — as duas aws_iam_role_policy (lambda.tf,
# lambda-account-trigger.tf) e, pior, as duas aws_lambda_function
# inteiras (env var DynamoDb__TableName), cujo código é publicado fora
# do Terraform via CI (update-function-code) e seria revertido pro zip
# local ao recriar. Com -replace, só a tabela é destruída/recriada; os
# demais recursos levam apenas um update in place (o ARN/nome da
# tabela não muda — sem sufixo aleatório —, então o valor final é
# idêntico ao anterior).
#
# Uso: ./recreate-table.sh (a partir de
# backend/infra/terraform/environments/hom, com terraform já
# inicializado — ver backend/infra/terraform/README.md — e credenciais
# AWS com permissão sobre a tabela GastosApp-Hom)
#
# ATENÇÃO: apaga TODOS OS DADOS da tabela de homologação. Ação
# irreversível — pede confirmação antes de aplicar.
set -e

TARGET="aws_dynamodb_table.gastos_app"

echo "Isso vai APAGAR TODOS OS DADOS da tabela GastosApp-Hom e recriá-la vazia."
echo "Alvo: $TARGET (ambiente: hom)"
read -r -p "Confirma? Digite 'sim' para continuar: " CONFIRM
if [ "$CONFIRM" != "sim" ]; then
  echo "Cancelado."
  exit 1
fi

terraform apply -replace="$TARGET"
