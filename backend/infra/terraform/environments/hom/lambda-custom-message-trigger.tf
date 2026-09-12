# Lambda .NET Native AOT do trigger CustomMessage do Cognito (FEAT-34) —
# ambiente de homologação. Substitui o corpo/assunto padrão dos e-mails de
# SignUp/ResendCode/ForgotPassword pelos templates HTML com a marca do
# jrn.expenses; o envio em si continua sendo feito pelo próprio Cognito,
# via SES (FEAT-33). Artefato próprio, build separado do da API e do
# account-trigger (ver infra/lambda/Dockerfile.build-custom-message-trigger/
# build-custom-message-trigger.sh).
#
# Papel de execução mínimo de todo o projeto até agora: só CloudWatch
# Logs — sem dynamodb:*/cognito-idp:*/ses:* (este trigger não lê/escreve
# no DynamoDB, não chama a API do Cognito de volta, e nunca envia e-mail
# diretamente — quem envia continua sendo o Cognito).

resource "aws_iam_role" "custom_message_trigger_lambda_exec" {
  name = "jrnexpenses-custom-message-trigger-lambda-exec-hom"

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
  name              = "/aws/lambda/jrnexpenses-custom-message-trigger-hom"
  retention_in_days = 7 # FEAT-38 — retenção explícita menor em hom (prod segue em 14)
}

resource "aws_iam_role_policy" "custom_message_trigger_lambda_exec" {
  name = "jrnexpenses-custom-message-trigger-lambda-exec-hom"
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
  function_name = "jrnexpenses-custom-message-trigger-hom"

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
