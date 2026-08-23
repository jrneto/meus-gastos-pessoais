variable "aws_region" {
  description = "Região AWS."
  type        = string
  default     = "us-east-1"
}

variable "aws_account_id" {
  description = "Conta AWS do projeto — usada para montar ARNs das funções Lambda."
  type        = string
  default     = "648443184523"
}

variable "github_org_repo" {
  description = "Repositório GitHub autorizado a assumir a Role via OIDC, no formato org/repo."
  type        = string
  default     = "jrneto/meus-gastos-pessoais"
}

variable "hom_function_name" {
  description = "Nome da função Lambda da API de homologação (backend/infra/terraform/environments/hom/)."
  type        = string
  default     = "gastos-app-api-hom"
}

variable "prod_function_name" {
  description = "Nome da função Lambda da API de produção (backend/infra/terraform/environments/prod/)."
  type        = string
  default     = "gastos-app-api"
}

variable "hom_account_trigger_function_name" {
  description = "Nome da função Lambda do trigger PostConfirmation do Cognito, homologação (FEAT-19)."
  type        = string
  default     = "jrnexpenses-account-trigger-hom"
}

variable "prod_account_trigger_function_name" {
  description = "Nome da função Lambda do trigger PostConfirmation do Cognito, produção (FEAT-19)."
  type        = string
  default     = "jrnexpenses-account-trigger"
}
