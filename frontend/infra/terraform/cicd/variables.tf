variable "aws_region" {
  description = "Região AWS."
  type        = string
  default     = "us-east-1"
}

variable "aws_account_id" {
  description = "Conta AWS do projeto — usada para montar ARNs de distribuição CloudFront."
  type        = string
  default     = "648443184523"
}

variable "github_org_repo" {
  description = "Repositório GitHub autorizado a assumir a Role via OIDC, no formato org/repo."
  type        = string
  default     = "jrneto/meus-gastos-pessoais"
}

variable "hom_bucket_name" {
  description = "Bucket S3 do frontend de homologação (frontend/infra/terraform/environments/hom/)."
  type        = string
  default     = "gastosapp-frontend-hom"
}

variable "prod_bucket_name" {
  description = "Bucket S3 do frontend de produção (frontend/infra/terraform/environments/prod/)."
  type        = string
  default     = "gastosapp-frontend-prod"
}

variable "hom_distribution_id" {
  description = "ID da distribuição CloudFront de homologação (frontend/infra/terraform/environments/hom/)."
  type        = string
  default     = "ELE195A1APCLB"
}

variable "prod_distribution_id" {
  description = "ID da distribuição CloudFront de produção (frontend/infra/terraform/environments/prod/, ver README.md)."
  type        = string
  default     = "E2YCZNS0F94SCU"
}
