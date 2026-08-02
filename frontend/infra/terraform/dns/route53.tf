# Hosted zone criada manualmente, trazida para o Terraform como recurso
# gerenciado (não como data source) para permitir reconstrução em uma
# conta AWS nova no futuro. prevent_destroy evita exclusão acidental no
# dia a dia (mesmo padrão do bucket de state em
# backend/infra/terraform/bootstrap/main.tf).
resource "aws_route53_zone" "main" {
  name    = var.domain_name
  comment = "HostedZone created by Route53 Registrar" # valor original, criado pelo registro do domínio

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_route53_record" "apex_a" {
  zone_id = aws_route53_zone.main.zone_id
  name    = var.domain_name
  type    = "A"

  alias {
    name                   = data.terraform_remote_state.prod.outputs.cloudfront_domain_name
    zone_id                = data.terraform_remote_state.prod.outputs.cloudfront_hosted_zone_id
    evaluate_target_health = false
  }
}

resource "aws_route53_record" "apex_aaaa" {
  zone_id = aws_route53_zone.main.zone_id
  name    = var.domain_name
  type    = "AAAA"

  alias {
    name                   = data.terraform_remote_state.prod.outputs.cloudfront_domain_name
    zone_id                = data.terraform_remote_state.prod.outputs.cloudfront_hosted_zone_id
    evaluate_target_health = false
  }
}

resource "aws_route53_record" "www_a" {
  zone_id = aws_route53_zone.main.zone_id
  name    = "www.${var.domain_name}"
  type    = "A"

  alias {
    name                   = data.terraform_remote_state.prod.outputs.cloudfront_domain_name
    zone_id                = data.terraform_remote_state.prod.outputs.cloudfront_hosted_zone_id
    evaluate_target_health = false
  }
}

resource "aws_route53_record" "www_aaaa" {
  zone_id = aws_route53_zone.main.zone_id
  name    = "www.${var.domain_name}"
  type    = "AAAA"

  alias {
    name                   = data.terraform_remote_state.prod.outputs.cloudfront_domain_name
    zone_id                = data.terraform_remote_state.prod.outputs.cloudfront_hosted_zone_id
    evaluate_target_health = false
  }
}

# CNAMEs de validação DNS do certificado ACM do frontend (domínio raiz +
# www). O output acm_domain_validation_options já vem escopado ao
# certificado do frontend (environments/prod/acm.tf) — não inclui o
# certificado de api.jrnexpenses.com (contexto backend), então não há
# risco de colisão sem filtro adicional.
resource "aws_route53_record" "acm_validation" {
  for_each = {
    for dvo in data.terraform_remote_state.prod.outputs.acm_domain_validation_options :
    dvo.domain_name => dvo
  }

  zone_id = aws_route53_zone.main.zone_id
  name    = each.value.resource_record_name
  type    = each.value.resource_record_type
  ttl     = 300
  records = [each.value.resource_record_value]
}