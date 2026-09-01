output "dynamodb_table_name" {
  description = "Nome da tabela DynamoDB provisionada."
  value       = aws_dynamodb_table.gastos_app.name
}

output "dynamodb_table_arn" {
  description = "ARN da tabela DynamoDB provisionada."
  value       = aws_dynamodb_table.gastos_app.arn
}

output "api_gateway_url" {
  description = "URL pública base do HTTP API."
  value       = aws_apigatewayv2_stage.default.invoke_url
}

output "api_custom_domain_url" {
  description = "URL pública da API através do domínio customizado."
  value       = "https://${aws_apigatewayv2_domain_name.api.domain_name}"
}

output "ses_domain_identity_arn" {
  description = "ARN da identidade de domínio SES verificada."
  value       = aws_ses_domain_identity.main.arn
}

output "ses_sender_email" {
  description = "Remetente usado pelo Cognito e pelas Lambdas para envio via SES."
  value       = aws_cognito_user_pool.main.email_configuration[0].from_email_address
}
