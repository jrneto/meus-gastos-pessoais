# Lambda .NET Native AOT da API do GastosApp — ambiente de
# homologação. Mesmo artefato (infra/lambda/function.zip) e mesmo
# código de produção — a única diferença é a variável de ambiente
# ParameterStore__Path, que isola a leitura do Parameter Store no
# prefixo /GastosApp/Hom/ (ver FEAT-13).

data "aws_caller_identity" "current" {}

resource "aws_iam_role" "lambda_exec" {
  name = "gastos-app-api-lambda-exec-hom"

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

resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/gastos-app-api-hom"
  retention_in_days = 14
}

resource "aws_iam_role_policy" "lambda_exec" {
  name = "gastos-app-api-lambda-exec-hom"
  role = aws_iam_role.lambda_exec.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "DynamoDbAccess"
        Effect = "Allow"
        Action = [
          "dynamodb:PutItem",
          "dynamodb:GetItem",
          "dynamodb:Query",
          "dynamodb:UpdateItem",
          "dynamodb:DeleteItem",
          "dynamodb:TransactWriteItems"
        ]
        Resource = [
          aws_dynamodb_table.gastos_app.arn,
          "${aws_dynamodb_table.gastos_app.arn}/index/*"
        ]
      },
      {
        Sid      = "ParameterStoreAccess"
        Effect   = "Allow"
        Action   = ["ssm:GetParametersByPath"]
        Resource = "arn:aws:ssm:${var.aws_region}:${data.aws_caller_identity.current.account_id}:parameter/GastosApp/Hom/*"
      },
      {
        Sid    = "CognitoAccess"
        Effect = "Allow"
        Action = [
          "cognito-idp:SignUp",
          "cognito-idp:InitiateAuth",
          "cognito-idp:GetUser",
          "cognito-idp:AdminDeleteUser"
        ]
        Resource = aws_cognito_user_pool.main.arn
      },
      {
        Sid    = "LogsAccess"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "${aws_cloudwatch_log_group.lambda.arn}:*"
      },
      {
        Sid      = "SesSendEmail"
        Effect   = "Allow"
        Action   = ["ses:SendEmail", "ses:SendRawEmail"]
        Resource = aws_ses_domain_identity.main.arn
      }
    ]
  })
}

resource "aws_lambda_function" "api" {
  function_name = "gastos-app-api-hom"

  filename         = "${path.module}/../../../lambda/function.zip"
  source_code_hash = filebase64sha256("${path.module}/../../../lambda/function.zip")

  role    = aws_iam_role.lambda_exec.arn
  handler = "bootstrap"
  runtime = "provided.al2023"

  architectures = ["x86_64"]
  memory_size   = 256
  timeout       = 10

  environment {
    variables = {
      ParameterStore__Path = "/GastosApp/Hom/"
      DynamoDb__TableName  = aws_dynamodb_table.gastos_app.name
    }
  }

  depends_on = [aws_cloudwatch_log_group.lambda]
}
