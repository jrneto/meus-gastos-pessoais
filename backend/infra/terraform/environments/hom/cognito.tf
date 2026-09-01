# Cognito User Pool + App Client — ambiente de homologação, isolado do
# User Pool de produção (FEAT-13). Mesma configuração de política de
# senha e fluxos de autenticação de produção.

resource "aws_cognito_user_pool" "main" {
  name = "user-pool-gastos-app-hom"

  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]

  password_policy {
    minimum_length                   = 8
    require_uppercase                = true
    require_lowercase                = true
    require_numbers                  = true
    require_symbols                  = true
    temporary_password_validity_days = 7
  }

  mfa_configuration   = "OFF"
  deletion_protection = "ACTIVE"

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
    recovery_mechanism {
      name     = "verified_phone_number"
      priority = 2
    }
  }

  schema {
    name                = "email"
    attribute_data_type = "String"
    required            = true
    mutable             = true

    string_attribute_constraints {
      min_length = "0"
      max_length = "2048"
    }
  }

  # Cria Account+Membership (Titular) assim que o usuário confirma o
  # cadastro (FEAT-19) — ver lambda-account-trigger.tf.
  lambda_config {
    post_confirmation = aws_lambda_function.account_trigger.arn
  }

  # Envio de e-mail (cadastro, recuperação de senha) via SES com
  # domínio próprio, em vez do envio padrão do Cognito (FEAT-33). O
  # depends_on garante que o Cognito só é reconfigurado depois da
  # identidade estar VERIFICADA (não só criada) — ver ses.tf.
  email_configuration {
    email_sending_account = "DEVELOPER"
    source_arn            = aws_ses_domain_identity.main.arn
    from_email_address    = "jrn.expenses (homologação) <no-reply@hom.jrnexpenses.com>"
  }

  depends_on = [aws_ses_domain_identity_verification.main]
}

resource "aws_cognito_user_pool_client" "spa" {
  name         = "controle-gastos-spa-hom"
  user_pool_id = aws_cognito_user_pool.main.id

  generate_secret = false

  explicit_auth_flows = [
    "ALLOW_REFRESH_TOKEN_AUTH",
    "ALLOW_USER_AUTH",
    "ALLOW_USER_PASSWORD_AUTH",
    "ALLOW_USER_SRP_AUTH",
  ]

  supported_identity_providers = ["COGNITO"]

  allowed_oauth_flows                  = ["code"]
  allowed_oauth_scopes                 = ["email", "openid", "phone"]
  allowed_oauth_flows_user_pool_client = true

  # Placeholder — não há frontend de homologação ainda (decisão
  # registrada em backend/specs/FEAT-13-ambiente-homologacao/plan.md).
  # Trocar quando existir um frontend de homologação real.
  callback_urls = ["http://localhost:5173"]

  prevent_user_existence_errors = "ENABLED"
  enable_token_revocation       = true

  access_token_validity  = 60
  id_token_validity      = 60
  refresh_token_validity = 5

  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }

  auth_session_validity = 3
}
