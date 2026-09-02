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
# precisa desse valor à mão. Espelha o mesmo remetente já calculado pelo
# email_configuration do User Pool (ver output ses_sender_email).
resource "aws_ssm_parameter" "ses_sender_email" {
  name  = "/GastosApp/Ses/SenderEmail"
  type  = "String"
  value = aws_cognito_user_pool.main.email_configuration[0].from_email_address
}