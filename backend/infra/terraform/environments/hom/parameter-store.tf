# Parâmetros do Cognito de homologação no Parameter Store, sob o
# prefixo /GastosApp/Hom/ — isolado do prefixo de produção
# (/GastosApp/), lido pela Lambda de hom via a variável de ambiente
# ParameterStore__Path (ver lambda.tf e FEAT-13).

resource "aws_ssm_parameter" "cognito_user_pool_id" {
  name  = "/GastosApp/Hom/Cognito/UserPoolId"
  type  = "String"
  value = aws_cognito_user_pool.main.id
}

resource "aws_ssm_parameter" "cognito_client_id" {
  name  = "/GastosApp/Hom/Cognito/ClientId"
  type  = "String"
  value = aws_cognito_user_pool_client.spa.id
}

resource "aws_ssm_parameter" "cognito_region" {
  name  = "/GastosApp/Hom/Cognito/Region"
  type  = "String"
  value = var.aws_region
}

# Origem do frontend de homologação liberada no CORS da aplicação
# (Program.cs lê "Cors:ProductionOrigins" — mesma chave de produção,
# sob o prefixo /GastosApp/Hom/ deste ambiente. Ver
# backend/specs/FEAT-11-cors-producao/plan.md e
# frontend/specs/FEAT-08-ambiente-homologacao/). Antes desta feature,
# não existia frontend de homologação, então esse parâmetro não tinha
# sido criado (só o placeholder em frontend_origins/callback_urls, ver
# backend/infra/CLAUDE.md).
resource "aws_ssm_parameter" "cors_hom_origin_0" {
  name  = "/GastosApp/Hom/Cors/ProductionOrigins/0"
  type  = "String"
  value = "https://hom.jrnexpenses.com"
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
  name  = "/GastosApp/Hom/Ses/SenderEmail"
  type  = "String"
  value = "jrn.expenses (homologação) <no-reply@hom.jrnexpenses.com>"
}
