data "terraform_remote_state" "prod" {
  backend = "s3"

  config = {
    bucket = var.state_bucket
    key    = var.prod_state_key
    region = var.aws_region
  }
}

# State separado do de "prod" (não reaproveitado) — mantém os dois
# ambientes desacoplados: recriar hom não depende de nada do state de
# prod, e vice-versa.
data "terraform_remote_state" "hom" {
  backend = "s3"

  config = {
    bucket = var.state_bucket
    key    = var.hom_state_key
    region = var.aws_region
  }
}