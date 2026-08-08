# Certificado do domínio do frontend (jrnexpenses.com). Não confundir com o
# certificado de api.jrnexpenses.com, que pertence ao contexto backend.
resource "aws_acm_certificate" "frontend" {
  domain_name               = var.domain_name
  subject_alternative_names = ["www.${var.domain_name}"]
  validation_method         = "DNS"

  key_algorithm = "RSA_2048" # confirmado via aws acm describe-certificate (KeyAlgorithm: RSA-2048)

  lifecycle {
    create_before_destroy = true
  }
}
