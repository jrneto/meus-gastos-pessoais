# WAF WebACL dedicado de homologação, coberto pelo plano flat-rate Free
# da distribuição (2º dos 3 planos Free disponíveis na conta — produção
# já usa 1). Mesmos 3 AWS Managed Rule Groups já usados em
# environments/prod/waf.tf, para manter a mesma postura de segurança.
# A assinatura da distribuição ao plano Free em si é um passo manual
# (ver frontend/infra/terraform/README.md) — o Terraform aqui só cria o
# WebACL e associa à distribuição (cloudfront.tf).
resource "aws_wafv2_web_acl" "hom" {
  name  = "gastosapp-hom-web-acl"
  scope = "CLOUDFRONT" # exige provider em us-east-1

  default_action {
    allow {}
  }

  rule {
    name     = "AWS-AWSManagedRulesAmazonIpReputationList"
    priority = 0

    override_action {
      none {}
    }

    statement {
      managed_rule_group_statement {
        name        = "AWSManagedRulesAmazonIpReputationList"
        vendor_name = "AWS"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "AWS-AWSManagedRulesAmazonIpReputationList-hom"
      sampled_requests_enabled   = true
    }
  }

  rule {
    name     = "AWS-AWSManagedRulesCommonRuleSet"
    priority = 1

    override_action {
      none {}
    }

    statement {
      managed_rule_group_statement {
        name        = "AWSManagedRulesCommonRuleSet"
        vendor_name = "AWS"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "AWS-AWSManagedRulesCommonRuleSet-hom"
      sampled_requests_enabled   = true
    }
  }

  rule {
    name     = "AWS-AWSManagedRulesKnownBadInputsRuleSet"
    priority = 2

    override_action {
      none {}
    }

    statement {
      managed_rule_group_statement {
        name        = "AWSManagedRulesKnownBadInputsRuleSet"
        vendor_name = "AWS"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "AWS-AWSManagedRulesKnownBadInputsRuleSet-hom"
      sampled_requests_enabled   = true
    }
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "gastosapp-hom-web-acl"
    sampled_requests_enabled   = true
  }
}
