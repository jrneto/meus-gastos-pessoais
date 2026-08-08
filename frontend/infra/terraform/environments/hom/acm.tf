# Certificado do domínio de homologação (hom.jrnexpenses.com). Sem SAN
# (não há variante www para este ambiente, ver spec — "Fora do escopo").
resource "aws_acm_certificate" "hom" {
  domain_name       = var.hom_domain_name
  validation_method = "DNS"

  lifecycle {
    create_before_destroy = true
  }
}
