variable "aws_region" {
  description = "Região AWS onde os recursos são provisionados."
  type        = string
  default     = "us-east-1"
}

variable "table_name" {
  description = "Nome da tabela DynamoDB single-table do GastosApp."
  type        = string
  default     = "GastosApp"
}

variable "frontend_origin" {
  description = "Origem (URL) do frontend Angular permitida no CORS. Placeholder até o domínio existir."
  type        = string
  default     = "http://localhost:4200"
}
