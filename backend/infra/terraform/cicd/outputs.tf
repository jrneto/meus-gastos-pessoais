output "cicd_role_arn" {
  description = "ARN da Role assumida via OIDC pelos workflows de deploy do backend — usado como variável (não-segredo) nos GitHub Environments backend-hom/backend-prod."
  value       = aws_iam_role.backend_cicd.arn
}
