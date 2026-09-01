# Records DNS de api-hom.jrnexpenses.com, dentro da hosted zone
# jrnexpenses.com. (gerenciada pelo Terraform do frontend,
# frontend/infra/terraform/dns/). Referenciada aqui só por leitura
# (data source, por nome), sem duplicar ou gerenciar a zona em si —
# mesmo padrão já usado por api.jrnexpenses.com em produção (FEAT-12).

data "aws_route53_zone" "jrnexpenses" {
  name         = "jrnexpenses.com."
  private_zone = false
}

resource "aws_route53_record" "api_hom_acm_validation" {
  for_each = {
    for dvo in aws_acm_certificate.api_hom.domain_validation_options :
    dvo.domain_name => dvo
  }

  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = each.value.resource_record_name
  type    = each.value.resource_record_type
  ttl     = 300
  records = [each.value.resource_record_value]
}

# Diferente de produção (certificado já ISSUED, importado): aqui o
# certificado é novo, então o apply precisa esperar a validação DNS
# completar antes de associar o domínio customizado ao certificado.
resource "aws_acm_certificate_validation" "api_hom" {
  certificate_arn         = aws_acm_certificate.api_hom.arn
  validation_record_fqdns = [for r in aws_route53_record.api_hom_acm_validation : r.fqdn]
}

resource "aws_route53_record" "api_hom_a" {
  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = "api-hom.jrnexpenses.com"
  type    = "A"

  alias {
    name                   = aws_apigatewayv2_domain_name.api_hom.domain_name_configuration[0].target_domain_name
    zone_id                = aws_apigatewayv2_domain_name.api_hom.domain_name_configuration[0].hosted_zone_id
    evaluate_target_health = false
  }
}

# Verificação de domínio e DKIM da identidade SES (ses.tf, FEAT-33) —
# mesmos princípios acima: records na zona do frontend, referenciada só
# por leitura.
resource "aws_route53_record" "ses_verification" {
  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = "_amazonses.hom.jrnexpenses.com"
  type    = "TXT"
  ttl     = 300
  records = [aws_ses_domain_identity.main.verification_token]
}

resource "aws_route53_record" "ses_dkim" {
  count   = 3
  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = "${aws_ses_domain_dkim.main.dkim_tokens[count.index]}._domainkey.hom.jrnexpenses.com"
  type    = "CNAME"
  ttl     = 300
  records = ["${aws_ses_domain_dkim.main.dkim_tokens[count.index]}.dkim.amazonses.com"]
}
