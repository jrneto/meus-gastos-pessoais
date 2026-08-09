# Role assumida pelos workflows de deploy do backend
# (.github/workflows/backend-deploy-{hom,prod}.yml) via OIDC — nenhuma
# access key de longa duração é armazenada em secret do GitHub (ver
# backend/specs/FEAT-14-cicd-github-actions/spec.md, US6).
data "aws_iam_policy_document" "github_trust" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [data.aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    # Restringe quem pode assumir a Role: só os workflows que rodam
    # contra os GitHub Environments backend-hom/backend-prod — nomes
    # distintos dos Environments hom/prod já usados pelo frontend (ver
    # plan.md, decisão 4), para não competir pela mesma variável
    # CICD_ROLE_ARN.
    #
    # IMPORTANTE: quando um job especifica `environment:`, o GitHub
    # Actions troca o claim `sub` do token OIDC de
    # "ref:refs/heads/<branch>" para "environment:<nome>" — mesmo
    # achado já documentado em frontend/infra/terraform/cicd/iam-role.tf
    # (FEAT-09 do frontend).
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values = [
        "repo:${var.github_org_repo}:environment:backend-hom",
        "repo:${var.github_org_repo}:environment:backend-prod",
      ]
    }
  }
}

resource "aws_iam_role" "backend_cicd" {
  name               = "gastosapp-backend-cicd"
  assume_role_policy = data.aws_iam_policy_document.github_trust.json
}
