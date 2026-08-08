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
