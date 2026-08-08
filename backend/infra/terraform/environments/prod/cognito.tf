# Cognito User Pool + App Client do GastosApp.
# Recriados via Terraform (não importados) — o pool anterior
# ("User pool - ue86ti", us-east-1_cvKHaKo0g) foi criado manualmente e
# permanece intacto até o usuário decidir excluí-lo. Ver
# backend/specs/FEAT-09-terraform-cognito-parameter-store/plan.md.

resource "aws_cognito_user_pool" "main" {
  name = "user-pool-gastos-app"

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
}

resource "aws_cognito_user_pool_client" "spa" {
  name         = "controle-gastos-spa"
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

  # Placeholder — não há frontend ainda (decisão registrada no plan.md).
  callback_urls = ["https://d84l1y8p4kdic.cloudfront.net"]

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