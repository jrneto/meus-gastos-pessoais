# Bootstrap: cria o bucket S3 que guarda o state remoto da configuração
# principal (backend/infra/terraform/). Este módulo é aplicado uma única
# vez (ou raramente) e mantém o próprio state LOCAL — não há como o
# bucket de state gerenciar o state que o cria (problema do
# "ovo e a galinha"). Ver backend/infra/terraform/README.md para o
# passo a passo completo.

data "aws_caller_identity" "current" {}

locals {
  bucket_name = "${var.bucket_prefix}-${data.aws_caller_identity.current.account_id}"
}

resource "aws_s3_bucket" "terraform_state" {
  bucket = local.bucket_name

  # Proteção contra remoção acidental do bucket de state via terraform destroy.
  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_versioning" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}
