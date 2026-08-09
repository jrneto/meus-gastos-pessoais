# Reaproveita o Provider OIDC do GitHub Actions já existente na conta
# (criado para o frontend, ver frontend/infra/terraform/cicd/oidc.tf) —
# é um recurso único por conta/URL de emissor, não por contexto/app,
# então esta config só o referencia via `data`, nunca cria um segundo.
data "aws_iam_openid_connect_provider" "github" {
  url = "https://token.actions.githubusercontent.com"
}
