variable "aws_region" {
  description = "Região AWS. CloudFront/ACM (usado em CloudFront)/WAF (scope CLOUDFRONT) exigem us-east-1."
  type        = string
  default     = "us-east-1"
}

variable "hom_domain_name" {
  description = "Domínio do frontend de homologação. Sem variante www (ambiente interno, sem necessidade identificada)."
  type        = string
  default     = "hom.jrnexpenses.com"
}

variable "frontend_bucket_name" {
  description = "Nome do bucket S3 que serve o build estático do frontend de homologação via CloudFront."
  type        = string
  default     = "gastosapp-frontend-hom"
}
