# Permissão mínima necessária para publicar o artefato (function.zip) e
# atualizar as variáveis de ambiente de versão — só nas quatro funções
# Lambda deste projeto (API hom/prod + trigger de conta hom/prod, FEAT-19),
# nenhum outro recurso da conta.
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
      "arn:aws:lambda:${var.aws_region}:${var.aws_account_id}:function:${var.hom_account_trigger_function_name}",
      "arn:aws:lambda:${var.aws_region}:${var.aws_account_id}:function:${var.prod_account_trigger_function_name}",
    ]
  }

  # FEAT-29 — usado só pelo job "integration-tests"/"check-hom-integration-tests"
  # (backend-deploy-hom.yml, backend-deploy-prod.yml,
  # backend-integration-tests-prod.yml), nunca pela Lambda da aplicação:
  # confirma/remove a conta de teste dedicada criada a cada execução da
  # suíte contra hom/prod (ver
  # backend/specs/FEAT-29-testes-integrados/plan.md, "Setup e limpeza da
  # conta de teste").
  statement {
    sid    = "ManageIntegrationTestCognitoUser"
    effect = "Allow"
    actions = [
      "cognito-idp:AdminConfirmSignUp",
      "cognito-idp:AdminDeleteUser",
    ]
    resources = [
      data.aws_cognito_user_pools.hom.arns[0],
      data.aws_cognito_user_pools.prod.arns[0],
    ]
  }

  # FEAT-29 — mesmo job acima: limpeza direta dos itens que a conta de
  # teste cria (Account/Membership/categorias padrão/UserProfile/
  # CpfPointer — ver backend/docs/data-model.md), já que não existe
  # endpoint de exclusão de conta na API. Sem "Scan" nas ações
  # concedidas, coerente com a regra imutável de
  # backend/docs/constitution.md ("Sem Scan no DynamoDB").
  # FEAT-32 — GetItem incluído porque TestAccountFixture.SetupAsync passou
  # a resolver o AccountId com um GetItem direto no AccountPointer
  # (mesmo access pattern, mais barato que a Query já usada na limpeza),
  # reaproveitado tanto pela limpeza quanto pelo módulo Membros
  # (InviteAndAcceptAsync).
  statement {
    sid    = "ManageIntegrationTestDynamoDbItems"
    effect = "Allow"
    actions = [
      "dynamodb:GetItem",
      "dynamodb:Query",
      "dynamodb:DeleteItem",
      "dynamodb:BatchWriteItem",
    ]
    resources = [
      "arn:aws:dynamodb:${var.aws_region}:${var.aws_account_id}:table/${var.hom_table_name}",
      "arn:aws:dynamodb:${var.aws_region}:${var.aws_account_id}:table/${var.prod_table_name}",
    ]
  }

  # FEAT-29 — resolve o UserPoolId (usado no Parameter Store,
  # /GastosApp/{Hom/}Cognito/UserPoolId) a partir do mesmo prefixo já
  # lido pela aplicação (AwsParameterStoreExtensions), evitando duplicar
  # o valor como uma segunda fonte de verdade (ver plan.md, "Configuração
  # do runner").
  statement {
    sid    = "ReadIntegrationTestParameterStore"
    effect = "Allow"
    actions = [
      "ssm:GetParametersByPath",
    ]
    resources = [
      "arn:aws:ssm:${var.aws_region}:${var.aws_account_id}:parameter/GastosApp",
      "arn:aws:ssm:${var.aws_region}:${var.aws_account_id}:parameter/GastosApp/*",
      "arn:aws:ssm:${var.aws_region}:${var.aws_account_id}:parameter/GastosApp/Hom",
      "arn:aws:ssm:${var.aws_region}:${var.aws_account_id}:parameter/GastosApp/Hom/*",
    ]
  }
}

# Resolve os ARNs dos User Pools hom/prod pelo nome (Id é gerado pela
# AWS na criação — não dá pra construir o ARN só com o que este módulo
# já sabe, diferente das funções Lambda acima, cujo nome é escolhido por
# nós). cicd/ é um state Terraform separado de environments/{hom,prod}/
# (ver backend/infra/CLAUDE.md) — por isso data source em vez de
# referência direta ao resource.
data "aws_cognito_user_pools" "hom" {
  name = var.hom_cognito_user_pool_name
}

data "aws_cognito_user_pools" "prod" {
  name = var.prod_cognito_user_pool_name
}

resource "aws_iam_role_policy" "backend_cicd" {
  name   = "gastosapp-backend-cicd-deploy"
  role   = aws_iam_role.backend_cicd.id
  policy = data.aws_iam_policy_document.backend_cicd.json
}
