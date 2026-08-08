output "bucket_name" {
  description = "Nome do bucket S3 de state (use como valor de 'bucket' no backend da configuração principal)."
  value       = aws_s3_bucket.terraform_state.id
}

output "bucket_arn" {
  description = "ARN do bucket S3 de state."
  value       = aws_s3_bucket.terraform_state.arn
}
