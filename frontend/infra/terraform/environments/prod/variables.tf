variable "aws_region" {
  description = "Região AWS. CloudFront/ACM (usado em CloudFront)/WAF (scope CLOUDFRONT) exigem us-east-1."
  type        = string
  default     = "us-east-1"
}

variable "domain_name" {
  description = "Domínio principal do frontend."
  type        = string
  default     = "jrnexpenses.com"
}

variable "frontend_bucket_name" {
  description = "Nome do bucket S3 que serve o build estático do frontend via CloudFront."
  type        = string
  default     = "gastosapp-frontend-prod"
}
