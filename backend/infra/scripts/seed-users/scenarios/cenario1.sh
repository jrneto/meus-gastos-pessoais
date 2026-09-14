#!/bin/bash
# Dados do cenário 1 — sourced por seed-scenario.sh, sem lógica própria.
# Um titular com 3 convidados (um por nível de permissão) e 4 categorias
# de receita, cada uma com um valor mensal fixo.

SENHA_PADRAO="Teste@123"

TITULAR_EMAIL="titular@jrnexpenses.com"
TITULAR_CPF="91212904079"
TITULAR_TELEFONE="19 99999 9999"
TITULAR_NOME="titular"

# email|cpf|telefone|nome|role (role: Leitura|Lancar|Total)
CONVIDADOS=(
  "titular-convidado-leitura@jrnexpenses.com|24936812072|19 99999 9999|titular convidado leitura|Leitura"
  "titular-convidado-lancar@jrnexpenses.com|14108083008|19 99999 9999|titular convidado lancar|Lancar"
  "titular-convidado-total@jrnexpenses.com|93655882009|19 99999 9999|titular convidado total|Total"
)

# nome|valor_mensal_em_reais (o engine converte pra centavos — x100)
CATEGORIAS_RECEITA=(
  "Salario Titular|10000"
  "Salario Convidado|5000"
  "Vale alimentação Titular|1500"
  "Vale alimentação Convidado|1000"
)
