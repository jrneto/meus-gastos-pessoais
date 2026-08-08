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

    # Restringe quem pode assumir a Role: só os workflows que rodam contra
    # os GitHub Environments hom/prod, nunca um `repo:.../*` genérico que
    # aceitaria qualquer branch/PR.
    #
    # IMPORTANTE: quando um job especifica `environment:` (nosso caso, ver
    # .github/workflows/frontend-deploy-{hom,prod}.yml), o GitHub Actions
    # troca o claim `sub` do token OIDC de "ref:refs/heads/<branch>" para
    # "environment:<nome>" — descoberto na validação end-to-end da FEAT-09
    # (1º deploy real falhou com "Not authorized to perform:
    # sts:AssumeRoleWithWebIdentity" até essa correção). Ver
    # https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect#example-subject-claims
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values = [
        "repo:${var.github_org_repo}:environment:hom",
        "repo:${var.github_org_repo}:environment:prod",
      ]
    }
  }
}

resource "aws_iam_role" "frontend_cicd" {
  name               = "gastosapp-frontend-cicd"
  assume_role_policy = data.aws_iam_policy_document.github_trust.json
}
