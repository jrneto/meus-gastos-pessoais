data "terraform_remote_state" "prod" {
  backend = "s3"

  config = {
    bucket = var.state_bucket
    key    = var.prod_state_key
    region = var.aws_region
  }
}