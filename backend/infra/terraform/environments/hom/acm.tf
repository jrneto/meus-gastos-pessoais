# Certificado ACM de api-hom.jrnexpenses.com — recurso NOVO (diferente
# de produção, onde o certificado já existia e foi importado na
# FEAT-12). A validação DNS completa via aws_acm_certificate_validation
# em dns.tf.
resource "aws_acm_certificate" "api_hom" {
  domain_name       = "api-hom.jrnexpenses.com"
  validation_method = "DNS"

  lifecycle {
    create_before_destroy = true
  }
}
