variable "aws_region" {
  description = "Região AWS onde os recursos são provisionados."
  type        = string
  default     = "us-east-1"
}

variable "table_name" {
  description = "Nome da tabela DynamoDB single-table do GastosApp (ambiente de homologação)."
  type        = string
  default     = "GastosApp-Hom"
}

variable "frontend_origins" {
  description = "Origens (URLs) do frontend liberadas no CORS do API Gateway de homologação. Vazio por padrão — ainda não existe frontend de homologação."
  type        = list(string)
  default     = []
}
