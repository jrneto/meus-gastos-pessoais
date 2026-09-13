# Lê a plataforma do backend (DynamoDB, Cognito, SES, API Gateway) de
# hom, que passou a viver em terraform/environments/hom/ do
# infra-jrnexpenses (FEAT-40) — mesmo padrão já usado em
# frontend/infra/terraform/environments/hom/remote_state.tf.
data "terraform_remote_state" "infra" {
  backend = "s3"

  config = {
    bucket = var.state_bucket
    key    = var.infra_state_key
    region = var.aws_region
  }
}

locals {
  dynamodb_table_name       = data.terraform_remote_state.infra.outputs.dynamodb_table_name
  dynamodb_table_arn        = data.terraform_remote_state.infra.outputs.dynamodb_table_arn
  cognito_user_pool_arn     = data.terraform_remote_state.infra.outputs.cognito_user_pool_arn
  ses_domain_identity_arn   = data.terraform_remote_state.infra.outputs.ses_domain_identity_arn
  api_gateway_execution_arn = data.terraform_remote_state.infra.outputs.api_gateway_execution_arn
}
