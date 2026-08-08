# Certificado ACM de api.jrnexpenses.com, criado manualmente e trazido
# para o Terraform via import (sem criar/recriar/alterar o certificado).
# Ver backend/specs/FEAT-12-terraform-dominio-customizado-api/plan.md.

resource "aws_acm_certificate" "api" {
  domain_name       = "api.jrnexpenses.com"
  validation_method = "DNS"

  lifecycle {
    create_before_destroy = true
  }
}
