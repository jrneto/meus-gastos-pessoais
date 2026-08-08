# Tabela single-table do GastosApp — ambiente de homologação, isolada da
# tabela de produção. Mesmo modelo de dados (access patterns e item
# model documentados em backend/docs/architecture.md e
# backend/docs/data-model.md, válidos para os dois ambientes).
resource "aws_dynamodb_table" "gastos_app" {
  name         = var.table_name
  billing_mode = "PAY_PER_REQUEST"

  hash_key  = "PK"
  range_key = "SK"

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }

  attribute {
    name = "GSI1PK"
    type = "S"
  }

  attribute {
    name = "GSI1SK"
    type = "S"
  }

  attribute {
    name = "GSI2PK"
    type = "S"
  }

  global_secondary_index {
    name            = "GSI1"
    hash_key        = "GSI1PK"
    range_key       = "GSI1SK"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "GSI2"
    hash_key        = "GSI2PK"
    projection_type = "KEYS_ONLY"
  }
}
