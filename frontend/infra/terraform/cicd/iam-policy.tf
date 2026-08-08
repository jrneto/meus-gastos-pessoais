# Permissão mínima necessária para publicar o build e invalidar cache —
# só nos buckets/distribuições de hom e prod deste projeto, nenhum outro
# recurso da conta.
#
# STATUS: este recurso NÃO está no state desta config (mesma causa dos
# outros dois arquivos — ver frontend/infra/terraform/README.md, seção
# "cicd/"). Policy inline `gastosapp-frontend-cicd-deploy` criada
# manualmente no console, conferida visualmente como idêntica ao JSON
# gerado aqui.
data "aws_iam_policy_document" "frontend_cicd" {
  statement {
    sid    = "PublishToFrontendBuckets"
    effect = "Allow"
    actions = [
      "s3:PutObject",
      "s3:DeleteObject",
      "s3:ListBucket",
    ]
    resources = [
      "arn:aws:s3:::${var.hom_bucket_name}",
      "arn:aws:s3:::${var.hom_bucket_name}/*",
      "arn:aws:s3:::${var.prod_bucket_name}",
      "arn:aws:s3:::${var.prod_bucket_name}/*",
    ]
  }

  statement {
    sid    = "InvalidateFrontendDistributions"
    effect = "Allow"
    actions = [
      "cloudfront:CreateInvalidation",
    ]
    resources = [
      "arn:aws:cloudfront::${var.aws_account_id}:distribution/${var.hom_distribution_id}",
      "arn:aws:cloudfront::${var.aws_account_id}:distribution/${var.prod_distribution_id}",
    ]
  }
}

resource "aws_iam_role_policy" "frontend_cicd" {
  name   = "gastosapp-frontend-cicd-deploy"
  role   = aws_iam_role.frontend_cicd.id
  policy = data.aws_iam_policy_document.frontend_cicd.json
}
