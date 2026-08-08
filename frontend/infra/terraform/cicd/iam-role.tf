# Role assumida pelos workflows de deploy do frontend
# (.github/workflows/frontend-deploy-{hom,prod}.yml) via OIDC — nenhuma
# access key de longa duração é armazenada em secret do GitHub (ver
# frontend/specs/FEAT-09-cicd-github-actions/spec.md, US7).
#
# STATUS: este recurso NÃO está no state desta config (mesma causa do
# OIDC Provider em oidc.tf — ver frontend/infra/terraform/README.md,
# seção "cicd/"). Criado manualmente no console
# (arn:aws:iam::648443184523:role/gastosapp-frontend-cicd), trust
# policy conferida visualmente como idêntica à gerada aqui.
data "aws_iam_policy_document" "github_trust" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    # Restringe quem pode assumir a Role: só o workflow de hom (push em
    # develop) e o de prod (tag semântica de uma release), nunca um
    # `repo:.../*` genérico que aceitaria qualquer branch/PR.
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values = [
        "repo:${var.github_org_repo}:ref:refs/heads/develop",
        "repo:${var.github_org_repo}:ref:refs/tags/v*",
      ]
    }
  }
}

resource "aws_iam_role" "frontend_cicd" {
  name               = "gastosapp-frontend-cicd"
  assume_role_policy = data.aws_iam_policy_document.github_trust.json
}
