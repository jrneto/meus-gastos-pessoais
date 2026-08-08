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

variable "frontend_origins" {
  description = "Origens (URLs) do frontend de produção permitidas no CORS do API Gateway."
  type        = list(string)
  default     = ["https://jrnexpenses.com", "https://www.jrnexpenses.com"]
}
