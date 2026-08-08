output "dynamodb_table_name" {
  description = "Nome da tabela DynamoDB de homologação provisionada."
  value       = aws_dynamodb_table.gastos_app.name
}

output "dynamodb_table_arn" {
  description = "ARN da tabela DynamoDB de homologação provisionada."
  value       = aws_dynamodb_table.gastos_app.arn
}

output "api_gateway_url" {
  description = "URL pública base do HTTP API de homologação."
  value       = aws_apigatewayv2_stage.default.invoke_url
}

output "api_custom_domain_url" {
  description = "URL pública da API de homologação através do domínio customizado."
  value       = "https://${aws_apigatewayv2_domain_name.api_hom.domain_name}"
}
