# API Gateway (HTTP API) na frente da Lambda da API do GastosApp.
# Sem autorizador JWT no Gateway — autenticação continua só na
# aplicação (FEAT-01). Ver
# backend/specs/FEAT-10-deploy-lambda-aot-api-gateway/plan.md.

resource "aws_apigatewayv2_api" "main" {
  name          = "gastos-app-api"
  protocol_type = "HTTP"

  cors_configuration {
    allow_origins     = var.frontend_origins
    allow_methods     = ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
    allow_headers     = ["Authorization", "Content-Type"]
    allow_credentials = true # cookie httpOnly do refresh token (FEAT-15/FEAT-12)
  }
}

resource "aws_apigatewayv2_integration" "lambda" {
  api_id                 = aws_apigatewayv2_api.main.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.api.invoke_arn
  payload_format_version = "2.0"
}

resource "aws_apigatewayv2_route" "default" {
  api_id    = aws_apigatewayv2_api.main.id
  route_key = "ANY /{proxy+}"
  target    = "integrations/${aws_apigatewayv2_integration.lambda.id}"
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.main.id
  name        = "$default"
  auto_deploy = true

  default_route_settings {
    throttling_rate_limit  = 5
    throttling_burst_limit = 10
  }
}

resource "aws_lambda_permission" "apigateway" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.api.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.main.execution_arn}/*/*"
}