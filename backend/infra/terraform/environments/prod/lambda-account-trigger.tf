# Lambda .NET Native AOT do trigger Post Confirmation do Cognito
# (FEAT-19) — cria Account+Membership (Titular) assim que o usuário
# confirma o cadastro. Artefato próprio, build separado do da API (ver
# infra/lambda/Dockerfile.build-account-trigger/build-account-trigger.sh).
# Papel de execução mínimo: só DynamoDB, sem cognito-idp:* (o trigger não
# chama o Cognito de volta) e sem Parameter Store (configuração só via
# variável de ambiente — decisão registrada no plan.md da FEAT-19).

resource "aws_iam_role" "account_trigger_lambda_exec" {
  name = "jrnexpenses-account-trigger-lambda-exec"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Service = "lambda.amazonaws.com"
      }
      Action = "sts:AssumeRole"
    }]
  })
}

resource "aws_cloudwatch_log_group" "account_trigger_lambda" {
  name              = "/aws/lambda/jrnexpenses-account-trigger"
  retention_in_days = 14
}

resource "aws_iam_role_policy" "account_trigger_lambda_exec" {
  name = "jrnexpenses-account-trigger-lambda-exec"
  role = aws_iam_role.account_trigger_lambda_exec.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "DynamoDbAccess"
        Effect = "Allow"
        Action = [
          "dynamodb:PutItem",
          "dynamodb:GetItem",
          "dynamodb:TransactWriteItems"
        ]
        Resource = aws_dynamodb_table.gastos_app.arn
      },
      {
        Sid    = "LogsAccess"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "${aws_cloudwatch_log_group.account_trigger_lambda.arn}:*"
      }
    ]
  })
}

resource "aws_lambda_function" "account_trigger" {
  function_name = "jrnexpenses-account-trigger"

  filename         = "${path.module}/../../../lambda/account-trigger-function.zip"
  source_code_hash = filebase64sha256("${path.module}/../../../lambda/account-trigger-function.zip")

  role    = aws_iam_role.account_trigger_lambda_exec.arn
  handler = "bootstrap"
  runtime = "provided.al2023"

  architectures = ["x86_64"]
  memory_size   = 256
  timeout       = 10

  environment {
    variables = {
      DynamoDb__TableName = aws_dynamodb_table.gastos_app.name
    }
  }

  depends_on = [aws_cloudwatch_log_group.account_trigger_lambda]
}

# Concede ao Cognito permissão de invocar esta função como trigger
# PostConfirmation (aws_cognito_user_pool.main.lambda_config, em
# cognito.tf) — sem isso o Cognito recebe AccessDenied ao tentar invocar.
resource "aws_lambda_permission" "cognito_invoke_account_trigger" {
  statement_id  = "AllowCognitoInvokePostConfirmation"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.account_trigger.function_name
  principal     = "cognito-idp.amazonaws.com"
  source_arn    = aws_cognito_user_pool.main.arn
}
