variable "aws_region" {
  description = "Região AWS onde o bucket de state é criado."
  type        = string
  default     = "us-east-1"
}

variable "bucket_prefix" {
  description = "Prefixo do nome do bucket S3 de state (o sufixo é o Account ID, para garantir nome único globalmente)."
  type        = string
  default     = "gastosapp-terraform-state"
}
