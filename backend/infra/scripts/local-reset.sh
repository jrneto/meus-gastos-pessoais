#!/bin/bash
# Zera os dados do ambiente local do backend GastosApp: remove todos os
# itens da tabela GastosApp-Local no LocalStack e todos os usuários do
# User Pool no cognito-local, sem derrubar containers nem recriar
# tabela/User Pool/App Client/parâmetros (isso continua sendo papel de
# local-init.sh). Útil pra repetir um teste manual do zero sem
# reprovisionar o ambiente inteiro.
#
# Pré-requisitos: `docker compose up -d` já rodando e
# `./scripts/local-init.sh` já executado ao menos uma vez.
#
# Uso: ./scripts/local-reset.sh (a partir de backend/infra/)
set -e

# Credenciais dummy exigidas pelo AWS CLI para falar com LocalStack/
# cognito-local — não são segredo, não têm relação com credenciais AWS
# reais (mesmo padrão usado por local-init.sh).
export AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-test}"
export AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-test}"
export AWS_DEFAULT_REGION="${AWS_DEFAULT_REGION:-us-east-1}"

# Ver comentário equivalente em local-init.sh — evita que o Git Bash/MSYS
# (Windows) corrompa nomes/paths passados ao AWS CLI.
export MSYS_NO_PATHCONV=1

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

"$SCRIPT_DIR/reset-dynamodb.sh"
"$SCRIPT_DIR/reset-cognito.sh"

echo ""
echo "Dados locais zerados (tabela e User Pool continuam existindo)."
