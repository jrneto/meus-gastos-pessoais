variable "aws_region" {
  description = "Região AWS."
  type        = string
  default     = "us-east-1"
}

variable "frontend_bucket_name" {
  description = "Nome do bucket S3 que serve o build estático do frontend de homologação via CloudFront."
  type        = string
  default     = "gastosapp-frontend-hom"
}

variable "state_bucket" {
  description = "Bucket S3 de state remoto, reaproveitado do backend (backend/infra/terraform/bootstrap/)."
  type        = string
  default     = "gastosapp-terraform-state-648443184523"
}

variable "infra_state_key" {
  description = "Key do state de terraform/environments/hom/ no repositório infra-jrnexpenses (CloudFront/OAC/ACM/WAF), lida via terraform_remote_state."
  type        = string
  default     = "infra-jrnexpenses/hom/terraform.tfstate"
}
