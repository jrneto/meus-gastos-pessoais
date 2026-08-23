# Lambda .NET Native AOT do trigger Post Confirmation do Cognito
# (FEAT-19) — ambiente de homologação. Mesmo artefato/papel de produção,
# tabela e User Pool isolados de hom.

resource "aws_iam_role" "account_trigger_lambda_exec" {
  name = "jrnexpenses-account-trigger-lambda-exec-hom"

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
  name              = "/aws/lambda/jrnexpenses-account-trigger-hom"
  retention_in_days = 14
}

resource "aws_iam_role_policy" "account_trigger_lambda_exec" {
  name = "jrnexpenses-account-trigger-lambda-exec-hom"
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
  function_name = "jrnexpenses-account-trigger-hom"

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

resource "aws_lambda_permission" "cognito_invoke_account_trigger" {
  statement_id  = "AllowCognitoInvokePostConfirmation"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.account_trigger.function_name
  principal     = "cognito-idp.amazonaws.com"
  source_arn    = aws_cognito_user_pool.main.arn
}
