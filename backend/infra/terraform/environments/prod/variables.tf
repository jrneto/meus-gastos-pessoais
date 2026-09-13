variable "aws_region" {
  description = "Região AWS onde os recursos são provisionados."
  type        = string
  default     = "us-east-1"
}

variable "state_bucket" {
  description = "Bucket S3 de state remoto, reaproveitado do bootstrap/ (infra-jrnexpenses/terraform/bootstrap/, desde a FEAT-40)."
  type        = string
  default     = "gastosapp-terraform-state-648443184523"
}

variable "infra_state_key" {
  description = "Key do state de terraform/environments/prod/ no repositório infra-jrnexpenses (DynamoDB, Cognito, Parameter Store, SES, API Gateway, domínio api), lida via terraform_remote_state (FEAT-40)."
  type        = string
  default     = "infra-jrnexpenses/prod/terraform.tfstate"
}
