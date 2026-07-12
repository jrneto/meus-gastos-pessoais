output "dynamodb_table_name" {
  description = "Nome da tabela DynamoDB provisionada."
  value       = aws_dynamodb_table.gastos_app.name
}

output "dynamodb_table_arn" {
  description = "ARN da tabela DynamoDB provisionada."
  value       = aws_dynamodb_table.gastos_app.arn
}
