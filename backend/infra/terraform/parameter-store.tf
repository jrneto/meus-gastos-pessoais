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