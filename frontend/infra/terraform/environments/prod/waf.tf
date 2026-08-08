# WebACL criado automaticamente pelo console ao habilitar proteção de
# segurança na criação da distribuição CloudFront. Sem custo adicional
# (plano CloudFront Free) — importado como está, nenhuma regra é
# adicionada/removida/alterada.
resource "aws_wafv2_web_acl" "frontend" {
  name  = "CreatedByCloudFront-8ee8deea"
  scope = "CLOUDFRONT" # exige provider em us-east-1
  # Sem description: o WebACL real não tem descrição (validação do
  # provider não aceita string vazia — omitir é o único jeito de
  # reproduzir "sem descrição").

  default_action {
    allow {}
  }

  # Prioridades e metric names confirmados via aws wafv2 get-web-acl.

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
      metric_name                = "AWS-AWSManagedRulesAmazonIpReputationList"
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
      metric_name                = "AWS-AWSManagedRulesCommonRuleSet"
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
      metric_name                = "AWS-AWSManagedRulesKnownBadInputsRuleSet"
      sampled_requests_enabled   = true
    }
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "CreatedByCloudFront-8ee8deea"
    sampled_requests_enabled   = true
  }
}