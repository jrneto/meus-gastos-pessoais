# Identidade de domínio SES de homologação (hom.jrnexpenses.com), com
# DKIM. Isolada da identidade de produção (subdomínio próprio), state
# próprio — ver
# backend/specs/FEAT-33-infra-email-transacional-ses/plan.md.

resource "aws_ses_domain_identity" "main" {
  domain = "hom.jrnexpenses.com"
}

resource "aws_ses_domain_dkim" "main" {
  domain = aws_ses_domain_identity.main.domain
}

# Só fica "success" depois que o record TXT de verificação (dns.tf)
# propagar — mesmo princípio do aws_acm_certificate_validation já usado
# no certificado de hom (FEAT-12).
resource "aws_ses_domain_identity_verification" "main" {
  domain     = aws_ses_domain_identity.main.id
  depends_on = [aws_route53_record.ses_verification]
}
