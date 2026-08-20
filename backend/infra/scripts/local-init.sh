#!/bin/bash
# Orquestra a inicialização do ambiente local do backend GastosApp
# (FEAT-18): cria o User Pool no cognito-local, a tabela no LocalStack
# e popula o Parameter Store local — nessa ordem, já que o Parameter
# Store depende dos IDs gerados pelo Cognito. Idempotente: pode ser
# rodado várias vezes sem duplicar recursos.
#
# Pré-requisitos: `docker compose up -d` já rodando (ver
# backend/infra/docker-compose.yml) e AWS CLI instalado.
#
# Uso: ./scripts/local-init.sh (a partir de backend/infra/)
set -e

# Credenciais dummy exigidas pelo AWS CLI para falar com LocalStack/
# cognito-local — não são segredo, não têm relação com credenciais AWS
# reais (mesmo padrão usado pela aplicação, ver appsettings.Development.json).
export AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-test}"
export AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-test}"
export AWS_DEFAULT_REGION="${AWS_DEFAULT_REGION:-us-east-1}"

# No Git Bash/MSYS (Windows), argumentos como "/GastosApp/..." são
# reescritos como caminho de arquivo Windows antes de chegar no AWS
# CLI, corrompendo o nome do parâmetro silenciosamente (put "funciona"
# mas grava em outro nome, get não encontra nada). Sem efeito em
# bash "de verdade" (Linux/macOS/WSL).
export MSYS_NO_PATHCONV=1

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

"$SCRIPT_DIR/init-cognito.sh"
"$SCRIPT_DIR/init-dynamodb.sh"
"$SCRIPT_DIR/init-parameter-store.sh"

echo ""
echo "Ambiente local pronto. Rode a API com:"
echo "  dotnet run --project ../src/GastosApp.Api"
