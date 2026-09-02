# Lambda .NET Native AOT do trigger CustomMessage do Cognito (FEAT-34) —
# produção. Ver environments/hom/lambda-custom-message-trigger.tf para o
# raciocínio completo (mesmo artefato físico, papel de execução mínimo
# só com CloudWatch Logs).

resource "aws_iam_role" "custom_message_trigger_lambda_exec" {
  name = "jrnexpenses-custom-message-trigger-lambda-exec"

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

resource "aws_cloudwatch_log_group" "custom_message_trigger_lambda" {
  name              = "/aws/lambda/jrnexpenses-custom-message-trigger"
  retention_in_days = 14
}

resource "aws_iam_role_policy" "custom_message_trigger_lambda_exec" {
  name = "jrnexpenses-custom-message-trigger-lambda-exec"
  role = aws_iam_role.custom_message_trigger_lambda_exec.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "LogsAccess"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "${aws_cloudwatch_log_group.custom_message_trigger_lambda.arn}:*"
      }
    ]
  })
}

resource "aws_lambda_function" "custom_message_trigger" {
  function_name = "jrnexpenses-custom-message-trigger"

  filename         = "${path.module}/../../../lambda/custom-message-trigger-function.zip"
  source_code_hash = filebase64sha256("${path.module}/../../../lambda/custom-message-trigger-function.zip")

  role    = aws_iam_role.custom_message_trigger_lambda_exec.arn
  handler = "bootstrap"
  runtime = "provided.al2023"

  architectures = ["x86_64"]
  memory_size   = 256
  timeout       = 10

  depends_on = [aws_cloudwatch_log_group.custom_message_trigger_lambda]
}

resource "aws_lambda_permission" "cognito_invoke_custom_message_trigger" {
  statement_id  = "AllowCognitoInvokeCustomMessage"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.custom_message_trigger.function_name
  principal     = "cognito-idp.amazonaws.com"
  source_arn    = aws_cognito_user_pool.main.arn
}
