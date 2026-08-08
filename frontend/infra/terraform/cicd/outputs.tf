output "cicd_role_arn" {
  description = "ARN da Role assumida via OIDC pelos workflows de deploy — usado como variável (não-segredo) nos GitHub Environments hom/prod."
  value       = aws_iam_role.frontend_cicd.arn
}

output "github_oidc_provider_arn" {
  description = "ARN do OIDC Provider do GitHub Actions nesta conta."
  value       = aws_iam_openid_connect_provider.github.arn
}
