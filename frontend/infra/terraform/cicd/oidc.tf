# Provider OIDC do GitHub Actions — permite que workflows assumam uma
# IAM Role sem credencial de longa duração (aws-actions/configure-aws-credentials).
#
# STATUS: este recurso NÃO está no state desta config. `apply` e
# `import` falharam com AccessDenied (iam:CreateOpenIDConnectProvider e
# iam:GetOpenIDConnectProvider negados para o perfil usado — ver
# frontend/infra/terraform/README.md, seção "cicd/"). O recurso real foi
# criado manualmente no console AWS
# (arn:aws:iam::648443184523:oidc-provider/token.actions.githubusercontent.com),
# byte a byte igual ao que este arquivo descreve. Mantido como
# documentação/referência para um `import` futuro, se a permissão for
# liberada.
resource "aws_iam_openid_connect_provider" "github" {
  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]

  # Thumbprint da CA intermediária usada pelo GitHub Actions para emitir
  # tokens OIDC (valor documentado pela AWS/GitHub). Desde a migração de CA
  # do GitHub em 2023, a AWS não valida mais esse valor na prática (aceita
  # qualquer thumbprint de 40 hex chars), mas o argumento continua
  # obrigatório no provider Terraform.
  thumbprint_list = [
    "1c58a3a8518e8759bf075b76b750d4f2df264fcd",
  ]
}
