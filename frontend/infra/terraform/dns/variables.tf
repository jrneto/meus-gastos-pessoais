variable "aws_region" {
  description = "Região AWS."
  type        = string
  default     = "us-east-1"
}

variable "domain_name" {
  description = "Domínio principal do frontend (hosted zone gerenciada aqui)."
  type        = string
  default     = "jrnexpenses.com"
}

variable "state_bucket" {
  description = "Bucket S3 de state remoto, reaproveitado do backend (backend/infra/terraform/bootstrap/)."
  type        = string
  default     = "gastosapp-terraform-state-648443184523"
}

variable "prod_state_key" {
  description = "Key do state da config environments/prod/, lida via terraform_remote_state."
  type        = string
  default     = "gastosapp-frontend/prod/terraform.tfstate"
}

variable "hom_state_key" {
  description = "Key do state da config environments/hom/, lida via terraform_remote_state."
  type        = string
  default     = "gastosapp-frontend/hom/terraform.tfstate"
}