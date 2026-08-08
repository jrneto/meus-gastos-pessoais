terraform {
  required_version = ">= 1.10" # necessário para locking nativo do backend S3 (use_lockfile)

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Configuração parcial: 'bucket' e 'region' não ficam fixos aqui porque
  # o nome do bucket é gerado no bootstrap/ (prefixo + Account ID) e pode
  # variar por conta AWS. Forneça os valores faltantes na hora do
  # 'terraform init' com -backend-config (ver backend/infra/terraform/README.md).
  backend "s3" {
    key          = "gastosapp/prod/terraform.tfstate"
    use_lockfile = true # locking nativo do backend S3 — dispensa tabela DynamoDB extra
  }
}

provider "aws" {
  region = var.aws_region
}
