# Identidade de domínio SES de produção (jrnexpenses.com), com DKIM.
# Base de envio de e-mail com marca própria para o Cognito e as Lambdas
# do backend que precisam enviar e-mail diretamente — ver
# backend/specs/FEAT-33-infra-email-transacional-ses/plan.md.

resource "aws_ses_domain_identity" "main" {
  domain = "jrnexpenses.com"
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
