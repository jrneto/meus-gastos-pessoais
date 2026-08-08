# Domínio customizado de api-hom.jrnexpenses.com na frente do HTTP API
# de homologação (aws_apigatewayv2_api.main, api-gateway.tf).
# certificate_arn referencia aws_acm_certificate_validation (não o
# certificado direto), forçando o Terraform a esperar a validação DNS
# terminar antes de associar o domínio.

resource "aws_apigatewayv2_domain_name" "api_hom" {
  domain_name = "api-hom.jrnexpenses.com"

  domain_name_configuration {
    certificate_arn = aws_acm_certificate_validation.api_hom.certificate_arn
    endpoint_type   = "REGIONAL"
    security_policy = "TLS_1_2"
  }
}

resource "aws_apigatewayv2_api_mapping" "api_hom" {
  api_id      = aws_apigatewayv2_api.main.id
  domain_name = aws_apigatewayv2_domain_name.api_hom.id
  stage       = aws_apigatewayv2_stage.default.id
}
