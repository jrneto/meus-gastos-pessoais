# Provider OIDC do GitHub Actions — já existe na conta (criado pra o
# frontend, ver frontend/infra/terraform/cicd/oidc.tf), reaproveitado
# aqui só pela ARN, nunca criado/lido de novo. Não usamos `data
# "aws_iam_openid_connect_provider"` (nem `resource`) porque tanto
# `iam:ListOpenIDConnectProviders` quanto `iam:GetOpenIDConnectProvider`
# são negados pro perfil usado pra aplicar este Terraform — mesmo com
# permissões de "Admin" (guardrail da conta contra escalonamento via
# trust policy de OIDC, não um erro de política — achado real ao
# aplicar a FEAT-19, mesmo gap já documentado no oidc.tf do frontend).
#
# A ARN é previsível (issuer + account id, formato fixo da AWS) e o
# provider é único por conta (não por contexto/app), então referenciar
# direto não corre risco de apontar pro recurso errado.
locals {
  github_oidc_provider_arn = "arn:aws:iam::${var.aws_account_id}:oidc-provider/token.actions.githubusercontent.com"
}
