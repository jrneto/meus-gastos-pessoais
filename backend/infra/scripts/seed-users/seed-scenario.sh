#!/bin/bash
# Popula um cenário de usuários fictícios (titular + convidados, categorias
# de receita e lançamentos de receita/despesa) no ambiente LOCAL do backend,
# via API HTTP real (mesmos endpoints que o frontend usa) — não escreve
# direto no DynamoDB/cognito-local. Só funciona contra localhost, nunca
# aponta pra hom/prod. Script de apoio pra teste manual, não é uma feature
# do produto (não segue /specify → /plan → /tasks).
#
# Pré-requisitos:
#   1) docker compose up -d (LocalStack + cognito-local, ver docker-compose.yml)
#   2) ./scripts/local-init.sh já rodado ao menos uma vez (cria o User Pool
#      local e grava backend/infra/.local-cognito-ids, lido por este script)
#   3) API rodando: dotnet run --project ../../src/GastosApp.Api
#      (porta padrão 5049, ver src/GastosApp.Api/Properties/launchSettings.json)
#
# Uso (a partir de backend/infra/):
#   ./scripts/seed-users/seed-scenario.sh <cenario>
#   ex.: ./scripts/seed-users/seed-scenario.sh cenario1
#
# Cada cenário vive em scripts/seed-users/scenarios/<nome>.sh — só dados
# (arrays bash), sem lógica. Ver scenarios/cenario1.sh como gabarito pra
# criar novos cenários.
#
# Idempotência: usuários, convites e categorias são seguros de recriar
# (a API retorna 409/422 pra duplicata e o script reaproveita o que já
# existe). LANÇAMENTOS NÃO SÃO IDEMPOTENTES — a API não tem chave natural
# pra deduplicar, então rodar o mesmo cenário duas vezes duplica todos os
# lançamentos. Rodando de novo sobre uma conta que já tem lançamentos, o
# script para e pede confirmação (ver guard_against_duplicate_transactions).
# Pra recomeçar do zero: ./scripts/reset-dynamodb.sh + ./scripts/reset-cognito.sh.
set -e

export MSYS_NO_PATHCONV=1
export AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-test}"
export AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-test}"
export AWS_DEFAULT_REGION="${AWS_DEFAULT_REGION:-us-east-1}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
API_BASE_URL="${API_BASE_URL:-http://localhost:5049}"
COGNITO_ENDPOINT="http://localhost:9229"
COGNITO_IDS_FILE="$SCRIPT_DIR/../../.local-cognito-ids"

SCENARIO="$1"
if [ -z "$SCENARIO" ]; then
  echo "Uso: $0 <cenario>  (ex.: $0 cenario1)" >&2
  echo "Cenários disponíveis:" >&2
  for f in "$SCRIPT_DIR"/scenarios/*.sh; do
    echo "  - $(basename "$f" .sh)" >&2
  done
  exit 1
fi

SCENARIO_FILE="$SCRIPT_DIR/scenarios/$SCENARIO.sh"
if [ ! -f "$SCENARIO_FILE" ]; then
  echo "Cenário '$SCENARIO' não encontrado ($SCENARIO_FILE)." >&2
  exit 1
fi

if [ ! -f "$COGNITO_IDS_FILE" ]; then
  echo "$COGNITO_IDS_FILE não encontrado — rode ./scripts/local-init.sh primeiro." >&2
  exit 1
fi
# shellcheck disable=SC1090
source "$COGNITO_IDS_FILE"  # USER_POOL_ID, CLIENT_ID

HEALTH_BODY=$(curl -s --max-time 5 "$API_BASE_URL/health" || true)
if ! echo "$HEALTH_BODY" | grep -q '"status":"ok"'; then
  echo "API local não responde em $API_BASE_URL — rode 'dotnet run --project ../../src/GastosApp.Api' primeiro." >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Categorias padrão de despesa: 13, ids fixos e iguais em qualquer ambiente
# (ver backend/specs/FEAT-28-seed-categorias-padrao/spec.md) — toda conta
# nova já nasce com elas, sem precisar criar via API. Hardcoded aqui em vez
# de descobertas via GET /categories: mais simples e mais barato (uma
# chamada HTTP a menos por usuário) já que os ids nunca mudam.
# ---------------------------------------------------------------------------
DEFAULT_DESPESA_CATEGORIES=(
  "862d8a7c-c3ef-412b-b4d3-88c1b4d317d9|Moradia"
  "369a308a-f96e-4ba9-ac43-3c9e8696141f|Alimentação"
  "a95ac718-1608-4c64-96da-4eefdc33e3e9|Transporte"
  "2644f155-1215-4936-8f9a-606e0ba58315|Saúde"
  "ceb83cec-9ca0-4ec0-a58f-adac83574faf|Educação"
  "f2d554c0-16d6-4fee-bef1-3364d9bb8ec3|Filhos e Dependentes"
  "24ef9ebc-58b3-4197-b9ac-1f203b79f07b|Lazer e Entretenimento"
  "0af4581d-37bf-4636-9805-ce2302403330|Vestuário e Cuidados Pessoais"
  "319ddec7-f867-427f-997a-66cd4ed9d8e1|Pets"
  "89bfe4ec-8747-44d3-92ba-4266960dd00f|Dívidas e Financiamentos"
  "961a8b3c-d210-4bd5-a470-1ef15c3549c3|Impostos, Taxas e Seguros"
  "d8865733-b002-4b11-b160-94237b2391c1|Doações e Presentes"
  "e9b32f2d-3eb7-4318-a268-438bb2d72f44|Outros"
)

DESPESA_DESCRICOES=(
  "Compra" "Pagamento" "Assinatura" "Serviço contratado" "Conta do mês"
  "Manutenção" "Compra do dia a dia" "Cobrança recorrente"
)

# ---------------------------------------------------------------------------
# Helpers de HTTP/JSON
# ---------------------------------------------------------------------------

aws_cognito() {
  aws --endpoint-url "$COGNITO_ENDPOINT" --region us-east-1 cognito-idp "$@"
}

# Extrai o valor de um campo string de um JSON "achatado" (sem objetos
# aninhados) — sem depender de jq, que não é pré-requisito deste projeto
# (mesmo trade-off já aceito em reset-dynamodb.sh).
json_field() {
  local body="$1" field="$2"
  echo "$body" | grep -oE "\"$field\": *\"[^\"]*\"" | head -1 | sed -E 's/^"[^"]+": *"//; s/"$//'
}

http_status() { echo "$1" | tail -n1; }
http_body() { echo "$1" | sed '$d'; }

# Duas armadilhas do Windows/Git Bash evitadas aqui, ambas validadas ao
# vivo nesta feature:
# 1) curl.exe recodifica argumentos de linha de comando pro codepage
#    ativo antes de montar o processo — um "-d '...Educação...'" direto
#    chega no servidor como Windows-1252, não UTF-8, e o
#    RegisterTransactionRequest/CreateCategoryRequest rejeita com 500
#    ("Cannot transcode invalid UTF-8 JSON text").
# 2) "--data-binary @arquivo" com MSYS_NO_PATHCONV=1 (exportado no topo
#    deste script, necessário pro AWS CLI não corromper "/GastosApp/...")
#    também desliga a conversão MSYS→Windows do caminho do arquivo, e
#    curl.exe não entende um path estilo "/tmp/..." sem essa conversão
#    ("error encountered when reading a file").
# "--data-binary @-" lendo de stdin (herestring "<<<") contorna as duas:
# não passa acento pelo argv nem precisa resolver um path de arquivo.
api_call() {
  local method="$1" path="$2" data="$3" token="$4"

  if [ -n "$token" ]; then
    curl -s -w '\n%{http_code}' -X "$method" "$API_BASE_URL$path" \
      -H "Content-Type: application/json" -H "Authorization: Bearer $token" \
      --data-binary @- <<< "$data"
  else
    curl -s -w '\n%{http_code}' -X "$method" "$API_BASE_URL$path" \
      -H "Content-Type: application/json" \
      --data-binary @- <<< "$data"
  fi
}

# ---------------------------------------------------------------------------
# Fluxo de negócio
# ---------------------------------------------------------------------------

# Registra (POST /auth/register) + confirma via cognito-local (mesmo padrão
# de TestAccountFixture em GastosApp.IntegrationTests — AdminConfirmSignUp
# em vez do OTP de /auth/confirm, já que não há e-mail real localmente) +
# loga (POST /auth/login). Idempotente: e-mail já registrado (409) pula
# pra login direto. Devolve o access token em stdout.
ensure_user_logged_in() {
  local email="$1" password="$2" name="$3" phone_raw="$4" cpf="$5"
  local phone="${phone_raw// /}"

  local resp status body
  resp=$(api_call POST /auth/register "$(printf '{"email":"%s","password":"%s","name":"%s","phoneNumber":"%s","cpf":"%s"}' \
    "$email" "$password" "$name" "$phone" "$cpf")" "")
  status=$(http_status "$resp")

  if [ "$status" = "201" ]; then
    echo "  [registrado] $email" >&2
    aws_cognito admin-confirm-sign-up --user-pool-id "$USER_POOL_ID" --username "$email" >/dev/null
  elif [ "$status" = "409" ]; then
    echo "  [já existia] $email" >&2
  else
    echo "  [ERRO] POST /auth/register $email -> $status: $(http_body "$resp")" >&2
    return 1
  fi

  resp=$(api_call POST /auth/login "$(printf '{"email":"%s","password":"%s"}' "$email" "$password")" "")
  status=$(http_status "$resp"); body=$(http_body "$resp")
  if [ "$status" != "200" ]; then
    echo "  [ERRO] POST /auth/login $email -> $status: $body" >&2
    return 1
  fi

  json_field "$body" accessToken
}

# Titular convida um e-mail (role: Leitura|Lancar|Total). Idempotente:
# convite/membro já existente (409) só loga e segue.
invite_member() {
  local titular_token="$1" email="$2" role="$3"
  local resp status
  resp=$(api_call POST /members "$(printf '{"email":"%s","role":"%s"}' "$email" "$role")" "$titular_token")
  status=$(http_status "$resp")
  if [ "$status" = "201" ]; then
    echo "  [convite criado] $email ($role)" >&2
  elif [ "$status" = "409" ]; then
    echo "  [convite/membro já existia] $email ($role)" >&2
  else
    echo "  [ERRO] POST /members $email -> $status: $(http_body "$resp")" >&2
    return 1
  fi
}

# Cria uma categoria de receita (role Total/Titular). Idempotente: nome
# duplicado volta 422 (CategoryErrors.NameConflict, não 409 — validado ao
# vivo) e o script busca o id já existente via GET /categories. Devolve o
# id em stdout.
create_or_get_receita_category() {
  local token="$1" nome="$2"
  local resp status body id
  resp=$(api_call POST /categories "$(printf '{"nome":"%s","tipo":"receita","orcamentoMensalCents":null}' "$nome")" "$token")
  status=$(http_status "$resp"); body=$(http_body "$resp")

  case "$status" in
    201)
      echo "  [categoria criada] $nome" >&2
      id=$(json_field "$body" id)
      ;;
    422)
      echo "  [categoria já existia] $nome" >&2
      id=$(find_category_id_by_name "$token" "$nome")
      ;;
    *)
      echo "  [ERRO] POST /categories '$nome' -> $status: $body" >&2
      return 1
      ;;
  esac

  echo "$id"
}

find_category_id_by_name() {
  local token="$1" nome="$2"
  curl -s -H "Authorization: Bearer $token" "$API_BASE_URL/categories" \
    | grep -oE '\{"id": *"[^}]*\}' \
    | grep -F "\"nome\":\"$nome\"" \
    | head -1 \
    | grep -oE '"id": *"[^"]*"' \
    | sed -E 's/^"id": *"//; s/"$//'
}

register_transaction() {
  local token="$1" description="$2" amount_cents="$3" category_id="$4" tipo="$5" date="$6"
  local resp status
  resp=$(api_call POST /transactions "$(printf '{"description":"%s","amountInCents":%s,"categoryId":"%s","tipo":"%s","date":"%s"}' \
    "$description" "$amount_cents" "$category_id" "$tipo" "$date")" "$token")
  status=$(http_status "$resp")
  if [ "$status" != "201" ]; then
    echo "  [ERRO] POST /transactions '$description' ($date) -> $status: $(http_body "$resp")" >&2
    return 1
  fi
}

# Lançamentos não são idempotentes (sem chave natural pra deduplicar, ver
# cabeçalho do arquivo) — se a conta do titular já tem algum lançamento,
# rodar de novo duplica tudo. Só um guard best-effort (checa titular, não
# cada convidado) pra evitar duplicação acidental por re-execução.
guard_against_duplicate_transactions() {
  local titular_token="$1"
  local body
  body=$(curl -s -H "Authorization: Bearer $titular_token" "$API_BASE_URL/transactions?Limit=1")
  if echo "$body" | grep -q '"items":\[{'; then
    echo ""
    echo "Aviso: esta conta já tem lançamentos. Rodar de novo VAI DUPLICAR" >&2
    echo "receitas/despesas (não há dedup). Zere antes com:" >&2
    echo "  ./scripts/reset-dynamodb.sh && ./scripts/reset-cognito.sh" >&2
    read -r -p "Continuar mesmo assim e duplicar? (s/N) " confirm
    if [ "$confirm" != "s" ] && [ "$confirm" != "S" ]; then
      echo "Abortado." >&2
      exit 1
    fi
  fi
}

random_amount_cents() {
  # Entre R$15,00 e R$195,00 — fictício, sem significado além de povoar dados.
  echo $(( (RANDOM % 18000) + 1500 ))
}

random_despesa_descricao() {
  local categoria_nome="$1"
  local idx=$(( RANDOM % ${#DESPESA_DESCRICOES[@]} ))
  echo "${DESPESA_DESCRICOES[$idx]} - $categoria_nome"
}

# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------

# shellcheck disable=SC1090
source "$SCENARIO_FILE"

echo "=== Cenário '$SCENARIO' — titular ==="
TITULAR_TOKEN=$(ensure_user_logged_in "$TITULAR_EMAIL" "$SENHA_PADRAO" "$TITULAR_NOME" "$TITULAR_TELEFONE" "$TITULAR_CPF")

echo ""
echo "=== Convidados ==="
declare -a LANCAR_TOKENS=()
for entry in "${CONVIDADOS[@]}"; do
  IFS='|' read -r email cpf telefone nome role <<< "$entry"
  invite_member "$TITULAR_TOKEN" "$email" "$role"
  token=$(ensure_user_logged_in "$email" "$SENHA_PADRAO" "$nome" "$telefone" "$cpf")
  if [ "$role" = "Lancar" ] || [ "$role" = "Total" ]; then
    LANCAR_TOKENS+=("$token")
  fi
done

echo ""
echo "=== Categorias de receita ==="
declare -a RECEITA_CATEGORY_IDS=()
for entry in "${CATEGORIAS_RECEITA[@]}"; do
  IFS='|' read -r nome _valor_reais <<< "$entry"
  id=$(create_or_get_receita_category "$TITULAR_TOKEN" "$nome")
  RECEITA_CATEGORY_IDS+=("$id")
done

guard_against_duplicate_transactions "$TITULAR_TOKEN"

TODAY=$(date +%Y-%m-%d)
START_DATE=$(date -d "$TODAY -3 months" +%Y-%m-01)

echo ""
echo "=== Lançamentos de receita (mensal, $START_DATE a $TODAY) ==="
m="$START_DATE"
while [ "$(date -d "$m" +%Y%m)" -le "$(date -d "$TODAY" +%Y%m)" ]; do
  target_date=$(date -d "$m +4 days" +%Y-%m-%d)  # dia 05 do mês
  if [ "$(date -d "$target_date" +%Y%m%d)" -gt "$(date -d "$TODAY" +%Y%m%d)" ]; then
    target_date="$TODAY"
  fi

  for i in "${!CATEGORIAS_RECEITA[@]}"; do
    IFS='|' read -r nome valor_reais <<< "${CATEGORIAS_RECEITA[$i]}"
    category_id="${RECEITA_CATEGORY_IDS[$i]}"
    amount_cents=$(( valor_reais * 100 ))
    register_transaction "$TITULAR_TOKEN" "$nome" "$amount_cents" "$category_id" "receita" "$target_date"
  done
  echo "  $target_date: ${#CATEGORIAS_RECEITA[@]} lançamentos de receita"

  m=$(date -d "$m +1 month" +%Y-%m-01)
done

echo ""
echo "=== Lançamentos de despesa do titular (diário, $START_DATE a $TODAY, 13 categorias/dia) ==="
d="$START_DATE"
while [ "$(date -d "$d" +%Y%m%d)" -le "$(date -d "$TODAY" +%Y%m%d)" ]; do
  for cat in "${DEFAULT_DESPESA_CATEGORIES[@]}"; do
    IFS='|' read -r category_id categoria_nome <<< "$cat"
    register_transaction "$TITULAR_TOKEN" "$(random_despesa_descricao "$categoria_nome")" "$(random_amount_cents)" "$category_id" "despesa" "$d"
  done
  echo "  $d: ${#DEFAULT_DESPESA_CATEGORIES[@]} lançamentos de despesa (titular)"
  d=$(date -d "$d +1 day" +%Y-%m-%d)
done

if [ "${#LANCAR_TOKENS[@]}" -gt 0 ]; then
  for convidado_token in "${LANCAR_TOKENS[@]}"; do
    echo ""
    echo "=== Lançamentos de despesa de um convidado (diário, $START_DATE a $TODAY) ==="
    d="$START_DATE"
    while [ "$(date -d "$d" +%Y%m%d)" -le "$(date -d "$TODAY" +%Y%m%d)" ]; do
      for cat in "${DEFAULT_DESPESA_CATEGORIES[@]}"; do
        IFS='|' read -r category_id categoria_nome <<< "$cat"
        register_transaction "$convidado_token" "$(random_despesa_descricao "$categoria_nome")" "$(random_amount_cents)" "$category_id" "despesa" "$d"
      done
      echo "  $d: ${#DEFAULT_DESPESA_CATEGORIES[@]} lançamentos de despesa (convidado)"
      d=$(date -d "$d +1 day" +%Y-%m-%d)
    done
  done
fi

echo ""
echo "Cenário '$SCENARIO' seedado com sucesso."
