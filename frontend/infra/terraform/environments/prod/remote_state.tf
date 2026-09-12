# Lê o ARN da distribuição CloudFront de prod, que passou a viver em
# terraform/environments/prod/ do infra-jrnexpenses (FEAT-34, etapa 4) —
# mesmo padrão já usado em environments/hom/remote_state.tf e em
# frontend/infra/terraform/dns/remote_state.tf.
data "terraform_remote_state" "infra" {
  backend = "s3"

  config = {
    bucket = var.state_bucket
    key    = var.infra_state_key
    region = var.aws_region
  }
}
