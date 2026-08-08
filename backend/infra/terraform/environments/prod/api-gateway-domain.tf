# Domínio customizado de api.jrnexpenses.com na frente do HTTP API
# (aws_apigatewayv2_api.main, api-gateway.tf), criado manualmente e
# trazido para o Terraform via import. Ver
# backend/specs/FEAT-12-terraform-dominio-customizado-api/plan.md.

resource "aws_apigatewayv2_domain_name" "api" {
  domain_name = "api.jrnexpenses.com"

  domain_name_configuration {
    certificate_arn = aws_acm_certificate.api.arn
    endpoint_type   = "REGIONAL"
    security_policy = "TLS_1_2"
  }
}

resource "aws_apigatewayv2_api_mapping" "api" {
  api_id      = aws_apigatewayv2_api.main.id
  domain_name = aws_apigatewayv2_domain_name.api.id
  stage       = aws_apigatewayv2_stage.default.id
}