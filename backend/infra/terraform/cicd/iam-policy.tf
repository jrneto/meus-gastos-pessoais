# Permissão mínima necessária para publicar o artefato (function.zip) e
# atualizar as variáveis de ambiente de versão — só nas duas funções
# Lambda deste projeto (hom/prod), nenhum outro recurso da conta.
data "aws_iam_policy_document" "backend_cicd" {
  statement {
    sid    = "UpdateBackendLambdaCode"
    effect = "Allow"
    actions = [
      "lambda:UpdateFunctionCode",
      "lambda:UpdateFunctionConfiguration",
      "lambda:GetFunction",
      "lambda:GetFunctionConfiguration",
    ]
    resources = [
      "arn:aws:lambda:${var.aws_region}:${var.aws_account_id}:function:${var.hom_function_name}",
      "arn:aws:lambda:${var.aws_region}:${var.aws_account_id}:function:${var.prod_function_name}",
    ]
  }
}

resource "aws_iam_role_policy" "backend_cicd" {
  name   = "gastosapp-backend-cicd-deploy"
  role   = aws_iam_role.backend_cicd.id
  policy = data.aws_iam_policy_document.backend_cicd.json
}
