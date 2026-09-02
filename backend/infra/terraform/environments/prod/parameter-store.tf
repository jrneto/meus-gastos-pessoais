# Parâmetros do Cognito no Parameter Store, lidos dinamicamente pelo
# backend em runtime (AwsParameterStoreExtensions / CognitoOptions).
# Estes 3 parâmetros já existiam manualmente e são trazidos para o
# Terraform via `terraform import` (não recriados) — ver
# backend/specs/FEAT-09-terraform-cognito-parameter-store/plan.md.

resource "aws_ssm_parameter" "cognito_user_pool_id" {
  name  = "/GastosApp/Cognito/UserPoolId"
  type  = "String"
  value = aws_cognito_user_pool.main.id
}

resource "aws_ssm_parameter" "cognito_client_id" {
  name  = "/GastosApp/Cognito/ClientId"
  type  = "String"
  value = aws_cognito_user_pool_client.spa.id
}

resource "aws_ssm_parameter" "cognito_region" {
  name  = "/GastosApp/Cognito/Region"
  type  = "String"
  value = var.aws_region
}

# Origens de produção do frontend liberadas no CORS da aplicação
# (Program.cs lê "Cors:ProductionOrigins", chave separada da lista de
# dev local — ver backend/specs/FEAT-11-cors-producao/plan.md).
# Parâmetros novos (não existiam manualmente), criação direta, sem
# `terraform import`.

resource "aws_ssm_parameter" "cors_production_origin_0" {
  name  = "/GastosApp/Cors/ProductionOrigins/0"
  type  = "String"
  value = "https://jrnexpenses.com"
}

resource "aws_ssm_parameter" "cors_production_origin_1" {
  name  = "/GastosApp/Cors/ProductionOrigins/1"
  type  = "String"
  value = "https://www.jrnexpenses.com"
}

# Remetente do e-mail de "senha alterada" (FEAT-36), enviado direto pela
# API via ses:SendEmail — fora do fluxo nativo do Cognito, então o backend
# precisa desse valor à mão. Mesmo literal já usado em
# email_configuration.from_email_address (cognito.tf) — não referencia o
# recurso ao vivo porque a AWS devolve esse atributo com encoding MIME
# (RFC 2047) sempre que o texto tem acentuação, diferente do literal do
# .tf; isso gera diff perpétuo nesse atributo (bug de idempotência do
# provider) e, pior, arrasta aws_lambda_function.custom_message_trigger
# pra dentro de qualquer apply targeted deste recurso (mesma cadeia de
# dependência), com risco de reverter o código da Lambda pro zip local
# desatualizado (deploy real acontece fora do Terraform, ver
# backend/infra/CLAUDE.md). Descoberto ao aplicar em hom (FEAT-36).
resource "aws_ssm_parameter" "ses_sender_email" {
  name  = "/GastosApp/Ses/SenderEmail"
  type  = "String"
  value = "jrn.expenses <no-reply@jrnexpenses.com>"
}